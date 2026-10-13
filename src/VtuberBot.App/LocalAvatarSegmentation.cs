using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace VtuberBot.App;

internal sealed record LocalAvatarSegmentationManifest(
    string ModelId,
    int Width,
    int Height,
    long SkinPixels,
    long ClothesPixels,
    long HairPixels,
    long DetailPixels);

internal sealed record LocalAvatarSegmentationResult(
    string OutputDirectory,
    string DetailsPath,
    string SkinPath,
    string ClothesPath,
    string HairPath,
    LocalAvatarSegmentationManifest Manifest);

/// <summary>
/// CPU-only, single-image segmentation worker. It is launched as a child copy of VTuberBot
/// so a Windows Job Object can impose a hard commit-memory limit without putting the main bot at risk.
/// Model: Kazuhito00/Skin-Clothes-Hair-Segmentation-using-SMP (MIT; experimental on anime art).
/// </summary>
internal static class LocalAvatarSegmentationWorker
{
    private const int ModelSize = 512;
    private const int MaxDecodedDimension = 1536;
    private const long MaxInputBytes = 25L * 1024 * 1024;
    private const long MaxInputPixels = 80_000_000;
    private const long ExpectedModelBytes = 8_671_872;
    private const string ModelId = "skin-clothes-hair-mobilenetv3-small-2024-09";
    private const string ExpectedGitBlobSha1 = "43c8e95fc3723b63f046c950631c282a523bf4f9";
    private const string ModelUrl =
        "https://raw.githubusercontent.com/Kazuhito00/Skin-Clothes-Hair-Segmentation-using-SMP/043fc08bf0ed845b36485a115085104c6459cbc4/02.model/DeepLabV3Plus%28timm-mobilenetv3_small_100%29_1366_2.16M_0.8297/best_model_simplifier.onnx";

    public static int Run(IReadOnlyList<string> args)
    {
        try
        {
            if (args.Count != 2)
                throw new ArgumentException("El trabajador local requiere la ruta de entrada y la carpeta de salida.");

            RunCoreAsync(args[0], args[1], CancellationToken.None).GetAwaiter().GetResult();
            return 0;
        }
        catch (Exception exception)
        {
            ReportError(exception.GetBaseException().Message);
            return 10;
        }
    }

    private static async Task RunCoreAsync(string inputPath, string outputDirectory, CancellationToken cancellationToken)
    {
        inputPath = Path.GetFullPath(inputPath);
        outputDirectory = Path.GetFullPath(outputDirectory);
        var inputInfo = new FileInfo(inputPath);
        if (!inputInfo.Exists || inputInfo.Length <= 0 || inputInfo.Length > MaxInputBytes)
            throw new InvalidDataException("La imagen debe existir y ocupar entre 1 byte y 25 MB.");
        if (!IsSupportedImage(inputPath))
            throw new InvalidDataException("Formatos permitidos: PNG, JPG, JPEG o BMP.");

        Directory.CreateDirectory(outputDirectory);
        Report("Validando y reduciendo la imagen de entrada...");
        var bitmap = LoadBoundedImage(inputPath);
        var width = bitmap.PixelWidth;
        var height = bitmap.PixelHeight;
        var stride = checked(width * 4);
        var sourcePixels = new byte[checked(stride * height)];
        bitmap.CopyPixels(sourcePixels, stride, 0);
        cancellationToken.ThrowIfCancellationRequested();

        Report("Buscando el modelo ligero (8,7 MB; solo se descarga la primera vez)...");
        var modelPath = await EnsureModelAsync(cancellationToken).ConfigureAwait(false);
        Report("Modelo local verificado. Configurando CPU de un solo hilo...");

        using var options = new SessionOptions
        {
            ExecutionMode = ExecutionMode.ORT_SEQUENTIAL,
            IntraOpNumThreads = 1,
            InterOpNumThreads = 1,
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_BASIC,
            EnableCpuMemArena = false,
            EnableMemoryPattern = false
        };
        using var session = new InferenceSession(modelPath, options);
        cancellationToken.ThrowIfCancellationRequested();

        Report("Separando piel, ropa y cabello a 512 × 512...");
        var inputTensor = BuildInputTensor(bitmap);
        var inputName = session.InputMetadata.Keys.First();
        using var modelInput = NamedOnnxValue.CreateFromTensor(inputName,
            new DenseTensor<float>(inputTensor, new[] { 1, 3, ModelSize, ModelSize }));
        using var outputs = session.Run(new[] { modelInput });
        var output = outputs.FirstOrDefault()
            ?? throw new InvalidDataException("El modelo no devolvió una máscara de segmentación.");
        var scores = output.AsTensor<float>().ToArray();
        var plane = ModelSize * ModelSize;
        if (scores.Length < 3 * plane)
            throw new InvalidDataException("El modelo devolvió una forma de datos inesperada.");

        var labels = ChooseClassLabels(scores, plane);
        var modelPixelCounts = labels.GroupBy(value => value).ToDictionary(group => group.Key, group => group.LongCount());
        if (modelPixelCounts.GetValueOrDefault((byte)1) +
            modelPixelCounts.GetValueOrDefault((byte)2) +
            modelPixelCounts.GetValueOrDefault((byte)3) < 100)
            throw new InvalidDataException("No se reconocieron suficientes zonas del personaje. Probá con una ilustración de cuerpo entero y un fondo simple.");

        AddDetailBorder(labels);
        cancellationToken.ThrowIfCancellationRequested();

        Report("Guardando capas PNG transparentes...");
        var detailsPath = Path.Combine(outputDirectory, "detalles-y-contorno.png");
        var skinPath = Path.Combine(outputDirectory, "piel-y-cuerpo.png");
        var clothesPath = Path.Combine(outputDirectory, "ropa.png");
        var hairPath = Path.Combine(outputDirectory, "cabello.png");

        var detailCount = SaveMaskedLayer(detailsPath, sourcePixels, labels, width, height, 4);
        var skinCount = SaveMaskedLayer(skinPath, sourcePixels, labels, width, height, 1);
        var clothesCount = SaveMaskedLayer(clothesPath, sourcePixels, labels, width, height, 2);
        var hairCount = SaveMaskedLayer(hairPath, sourcePixels, labels, width, height, 3);
        if (skinCount + clothesCount + hairCount < 100)
            throw new InvalidDataException("La segmentación no produjo capas útiles. No se creó un perfil.");

        var manifest = new LocalAvatarSegmentationManifest(
            ModelId, width, height, skinCount, clothesCount, hairCount, detailCount);
        await File.WriteAllTextAsync(
            Path.Combine(outputDirectory, "manifest.json"),
            JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }),
            cancellationToken).ConfigureAwait(false);

        Report($"TERMINADO\t{width} × {height}; piel {skinCount:N0}, ropa {clothesCount:N0}, cabello {hairCount:N0}, detalles {detailCount:N0} píxeles.");
    }

    private static bool IsSupportedImage(string path) =>
        Path.GetExtension(path).ToLowerInvariant() is ".png" or ".jpg" or ".jpeg" or ".bmp";

    private static BitmapSource LoadBoundedImage(string path)
    {
        int sourceWidth;
        int sourceHeight;
        using (var stream = File.OpenRead(path))
        {
            var decoder = BitmapDecoder.Create(
                stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
            if (decoder.Frames.Count == 0)
                throw new InvalidDataException("La imagen no contiene un cuadro válido.");

            sourceWidth = decoder.Frames[0].PixelWidth;
            sourceHeight = decoder.Frames[0].PixelHeight;
        }

        if (sourceWidth <= 0 || sourceHeight <= 0 ||
            (long)sourceWidth * sourceHeight > MaxInputPixels)
            throw new InvalidDataException("La imagen tiene demasiados píxeles. Usá una imagen de menos de 80 megapíxeles.");

        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.UriSource = new Uri(path, UriKind.Absolute);
        if (sourceWidth >= sourceHeight)
            image.DecodePixelWidth = Math.Min(sourceWidth, MaxDecodedDimension);
        else
            image.DecodePixelHeight = Math.Min(sourceHeight, MaxDecodedDimension);
        image.EndInit();
        image.Freeze();

        var converted = new FormatConvertedBitmap(image, PixelFormats.Bgra32, null, 0);
        converted.Freeze();
        return converted;
    }

    private static float[] BuildInputTensor(BitmapSource source)
    {
        var transform = new TransformedBitmap(
            source,
            new ScaleTransform(
                (double)ModelSize / source.PixelWidth,
                (double)ModelSize / source.PixelHeight));
        transform.Freeze();
        var converted = new FormatConvertedBitmap(transform, PixelFormats.Bgra32, null, 0);
        converted.Freeze();

        var stride = ModelSize * 4;
        var pixels = new byte[stride * ModelSize];
        converted.CopyPixels(pixels, stride, 0);
        var plane = ModelSize * ModelSize;
        var tensor = new float[3 * plane];

        const float meanR = 0.485f;
        const float meanG = 0.456f;
        const float meanB = 0.406f;
        const float stdR = 0.229f;
        const float stdG = 0.224f;
        const float stdB = 0.225f;
        for (var i = 0; i < plane; i++)
        {
            var offset = i * 4;
            var b = pixels[offset] / 255f;
            var g = pixels[offset + 1] / 255f;
            var r = pixels[offset + 2] / 255f;
            tensor[i] = (r - meanR) / stdR;
            tensor[plane + i] = (g - meanG) / stdG;
            tensor[2 * plane + i] = (b - meanB) / stdB;
        }

        return tensor;
    }

    // The exported model has three independent score maps in this order: skin, clothes, hair.
    private static byte[] ChooseClassLabels(float[] scores, int plane)
    {
        var labels = new byte[plane];
        for (var i = 0; i < plane; i++)
        {
            var skin = FiniteScore(scores[i]);
            var clothes = FiniteScore(scores[plane + i]);
            var hair = FiniteScore(scores[2 * plane + i]);
            var best = Math.Max(skin, Math.Max(clothes, hair));
            if (best < 0.50f)
                continue;

            labels[i] = skin >= clothes && skin >= hair ? (byte)1
                : clothes >= hair ? (byte)2
                : (byte)3;
        }

        return labels;
    }

    private static float FiniteScore(float score) =>
        float.IsFinite(score) ? Math.Clamp(score, 0f, 1f) : 0f;

    // Preserve thin line-art and small details just outside the three semantic classes.
    // These nearby pixels become an "other/details" layer rather than being thrown away.
    private static void AddDetailBorder(byte[] labels)
    {
        var original = (byte[])labels.Clone();
        const int radius = 4;
        for (var y = 0; y < ModelSize; y++)
        {
            for (var x = 0; x < ModelSize; x++)
            {
                var index = y * ModelSize + x;
                if (original[index] != 0)
                    continue;

                var found = false;
                for (var dy = -radius; dy <= radius && !found; dy++)
                {
                    var ny = y + dy;
                    if (ny < 0 || ny >= ModelSize) continue;
                    for (var dx = -radius; dx <= radius; dx++)
                    {
                        if (dx * dx + dy * dy > radius * radius) continue;
                        var nx = x + dx;
                        if (nx < 0 || nx >= ModelSize) continue;
                        if (original[ny * ModelSize + nx] == 0) continue;
                        found = true;
                        break;
                    }
                }

                if (found) labels[index] = 4;
            }
        }
    }

    private static long SaveMaskedLayer(
        string path, byte[] sourcePixels, byte[] labels, int width, int height, byte layerLabel)
    {
        var output = new byte[sourcePixels.Length];
        long selectedPixels = 0;
        for (var y = 0; y < height; y++)
        {
            var modelY = Math.Min(ModelSize - 1, (int)((long)y * ModelSize / height));
            for (var x = 0; x < width; x++)
            {
                var modelX = Math.Min(ModelSize - 1, (int)((long)x * ModelSize / width));
                var sourceOffset = (y * width + x) * 4;
                var label = labels[modelY * ModelSize + modelX];
                if (label != layerLabel || sourcePixels[sourceOffset + 3] == 0)
                    continue;

                output[sourceOffset] = sourcePixels[sourceOffset];
                output[sourceOffset + 1] = sourcePixels[sourceOffset + 1];
                output[sourceOffset + 2] = sourcePixels[sourceOffset + 2];
                output[sourceOffset + 3] = sourcePixels[sourceOffset + 3];
                selectedPixels++;
            }
        }

        var image = BitmapSource.Create(
            width, height, 96, 96, PixelFormats.Bgra32, null, output, checked(width * 4));
        image.Freeze();
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        using var file = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        encoder.Save(file);
        return selectedPixels;
    }

    private static async Task<string> EnsureModelAsync(CancellationToken cancellationToken)
    {
        var modelFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VtuberBot", "models");
        Directory.CreateDirectory(modelFolder);
        var modelPath = Path.Combine(modelFolder, "skin-clothes-hair-mobilenetv3-small.onnx");
        if (await IsExpectedModelAsync(modelPath, cancellationToken).ConfigureAwait(false))
            return modelPath;

        TryDelete(modelPath);
        var temporaryPath = modelPath + ".download";
        TryDelete(temporaryPath);
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
            using var request = new HttpRequestMessage(HttpMethod.Get, ModelUrl);
            request.Headers.UserAgent.ParseAdd("VTuberBot-Local-Segmentation/1.0");
            using var response = await client.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength is long contentLength &&
                contentLength > 12 * 1024 * 1024)
                throw new InvalidDataException("La descarga del modelo supera el tamaño permitido.");

            await using (var incoming = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
            await using (var outgoing = new FileStream(
                temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                var buffer = new byte[81920];
                long total = 0;
                while (true)
                {
                    var read = await incoming.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false);
                    if (read == 0) break;
                    total += read;
                    if (total > 12 * 1024 * 1024)
                        throw new InvalidDataException("La descarga del modelo supera el tamaño permitido.");
                    await outgoing.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                }
            }

            if (!await IsExpectedModelAsync(temporaryPath, cancellationToken).ConfigureAwait(false))
                throw new InvalidDataException("El modelo descargado no coincide con la versión publicada y fue rechazado.");

            File.Move(temporaryPath, modelPath, overwrite: true);
            return modelPath;
        }
        finally
        {
            TryDelete(temporaryPath);
        }
    }

    private static async Task<bool> IsExpectedModelAsync(string path, CancellationToken cancellationToken)
    {
        var info = new FileInfo(path);
        if (!info.Exists || info.Length != ExpectedModelBytes)
            return false;

        var hash = await ComputeGitBlobSha1Async(path, cancellationToken).ConfigureAwait(false);
        return string.Equals(hash, ExpectedGitBlobSha1, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<string> ComputeGitBlobSha1Async(string path, CancellationToken cancellationToken)
    {
        await using var file = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
        var header = Encoding.ASCII.GetBytes($"blob {file.Length}\0");
        hash.AppendData(header);
        var buffer = new byte[81920];
        while (true)
        {
            var read = await file.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false);
            if (read == 0) break;
            hash.AppendData(buffer.AsSpan(0, read));
        }

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static void Report(string message) => WriteLine("INFO\t" + message);
    private static void ReportError(string message)
    {
        WriteLine("ERROR\t" + message);
        try { Console.Error.WriteLine(message); Console.Error.Flush(); }
        catch { }
    }

    private static void WriteLine(string value)
    {
        try
        {
            Console.Out.WriteLine(value);
            Console.Out.Flush();
        }
        catch
        {
            // The worker's exit code is the primary error channel if redirected console output is unavailable.
        }
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch { }
    }
}

/// <summary>
/// Starts the local model in a separate copy of the app, below normal priority, with one ONNX CPU thread,
/// a ten-minute deadline, and a Windows Job Object enforcing a 2 GiB process-commit limit.
/// </summary>
internal static class LocalAvatarSegmentationRunner
{
    private const string WorkerSwitch = "--avatar-segmentation-worker";
    private const ulong MemoryLimitBytes = 2UL * 1024 * 1024 * 1024;

    public static async Task<LocalAvatarSegmentationResult> RunAsync(
        string inputPath,
        string outputDirectory,
        Action<string> report,
        CancellationToken cancellationToken)
    {
        var executablePath = Environment.ProcessPath
            ?? throw new InvalidOperationException("No se pudo encontrar el ejecutable actual para iniciar el trabajador local.");
        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            WorkingDirectory = AppContext.BaseDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        if (string.Equals(Path.GetFileNameWithoutExtension(executablePath), "dotnet", StringComparison.OrdinalIgnoreCase))
        {
            var assemblyPath = Assembly.GetEntryAssembly()?.Location;
            if (string.IsNullOrWhiteSpace(assemblyPath) || !File.Exists(assemblyPath))
                throw new InvalidOperationException("No se encontró el assembly para iniciar el trabajador local.");
            startInfo.ArgumentList.Add(assemblyPath);
        }

        startInfo.ArgumentList.Add(WorkerSwitch);
        startInfo.ArgumentList.Add(Path.GetFullPath(inputPath));
        startInfo.ArgumentList.Add(Path.GetFullPath(outputDirectory));

        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(10));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        using var process = new Process { StartInfo = startInfo };

        var job = CreateMemoryLimitedJob();
        try
        {
            if (!process.Start())
                throw new InvalidOperationException("No se pudo iniciar el trabajador de segmentación.");

            if (!AssignProcessToJobObject(job, process.Handle))
            {
                var error = Marshal.GetLastWin32Error();
                TryKill(process);
                throw new InvalidOperationException(
                    "Windows no permitió aplicar el límite de memoria de 2 GB. No se inició la IA sin ese límite. Código: " + error);
            }

            try { process.PriorityClass = ProcessPriorityClass.BelowNormal; }
            catch { /* Priority is best-effort; the memory and thread limits remain enforced. */ }

            var stdoutTask = PumpOutputAsync(process.StandardOutput, report, null);
            var stderrLines = new Queue<string>();
            var stderrTask = PumpOutputAsync(process.StandardError, _ => { }, stderrLines);
            try
            {
                await process.WaitForExitAsync(linked.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                TryKill(process);
                try { await process.WaitForExitAsync().ConfigureAwait(false); } catch { }
                if (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                    throw new TimeoutException("Se alcanzó el límite de 10 minutos. La IA se detuvo sin bloquear VTuberBot.");
                throw;
            }

            await Task.WhenAll(stdoutTask, stderrTask).ConfigureAwait(false);
            if (process.ExitCode != 0)
            {
                var error = string.Join(Environment.NewLine, stderrLines);
                var message = string.IsNullOrWhiteSpace(error)
                    ? "El trabajador local terminó con el código " + process.ExitCode + "."
                    : error;
                throw new InvalidOperationException(message);
            }
        }
        finally
        {
            TryKillIfRunning(process);
            CloseHandle(job); // KILL_ON_JOB_CLOSE makes cancellation and parent shutdown safe.
        }

        var manifestPath = Path.Combine(Path.GetFullPath(outputDirectory), "manifest.json");
        if (!File.Exists(manifestPath))
            throw new InvalidDataException("El trabajador terminó sin generar su manifiesto de capas.");

        await using var manifestStream = File.OpenRead(manifestPath);
        var manifest = await JsonSerializer.DeserializeAsync<LocalAvatarSegmentationManifest>(
            manifestStream, cancellationToken: CancellationToken.None).ConfigureAwait(false)
            ?? throw new InvalidDataException("No se pudo leer el manifiesto de capas.");

        var result = new LocalAvatarSegmentationResult(
            Path.GetFullPath(outputDirectory),
            Path.Combine(outputDirectory, "detalles-y-contorno.png"),
            Path.Combine(outputDirectory, "piel-y-cuerpo.png"),
            Path.Combine(outputDirectory, "ropa.png"),
            Path.Combine(outputDirectory, "cabello.png"),
            manifest);
        foreach (var layerPath in new[] { result.DetailsPath, result.SkinPath, result.ClothesPath, result.HairPath })
        {
            if (!File.Exists(layerPath) || new FileInfo(layerPath).Length == 0)
                throw new InvalidDataException("Falta una de las capas PNG generadas: " + Path.GetFileName(layerPath));
        }

        return result;
    }

    private static async Task PumpOutputAsync(StreamReader reader, Action<string> report, Queue<string>? errors)
    {
        while (await reader.ReadLineAsync().ConfigureAwait(false) is { } line)
        {
            if (errors is not null)
            {
                lock (errors)
                {
                    errors.Enqueue(line);
                    while (errors.Count > 20) errors.Dequeue();
                }
                continue;
            }

            if (line.StartsWith("INFO\t", StringComparison.Ordinal))
                report(line[5..]);
            else if (line.StartsWith("TERMINADO\t", StringComparison.Ordinal))
                report(line[10..]);
            else if (line.StartsWith("ERROR\t", StringComparison.Ordinal))
                report(line[6..]);
        }
    }

    private static IntPtr CreateMemoryLimitedJob()
    {
        var job = CreateJobObjectW(IntPtr.Zero, null);
        if (job == IntPtr.Zero)
            throw new InvalidOperationException("Windows no pudo crear el límite de memoria del trabajador. Código: " + Marshal.GetLastWin32Error());

        var limits = new JobObjectExtendedLimitInformation
        {
            BasicLimitInformation = new JobObjectBasicLimitInformation
            {
                LimitFlags = JobObjectLimitProcessMemory | JobObjectLimitKillOnJobClose
            },
            ProcessMemoryLimit = new UIntPtr(MemoryLimitBytes)
        };

        if (!SetInformationJobObject(
                job,
                JobObjectInfoClass.ExtendedLimitInformation,
                ref limits,
                (uint)Marshal.SizeOf<JobObjectExtendedLimitInformation>()))
        {
            var error = Marshal.GetLastWin32Error();
            CloseHandle(job);
            throw new InvalidOperationException("Windows no pudo fijar el límite de memoria de 2 GB. Código: " + error);
        }

        return job;
    }

    private static void TryKill(Process process)
    {
        try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
        catch { }
    }

    private static void TryKillIfRunning(Process process) => TryKill(process);

    private const uint JobObjectLimitProcessMemory = 0x00000100;
    private const uint JobObjectLimitKillOnJobClose = 0x00002000;

    private enum JobObjectInfoClass
    {
        ExtendedLimitInformation = 9
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JobObjectBasicLimitInformation
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public UIntPtr MinimumWorkingSetSize;
        public UIntPtr MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public UIntPtr Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IoCounters
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JobObjectExtendedLimitInformation
    {
        public JobObjectBasicLimitInformation BasicLimitInformation;
        public IoCounters IoInfo;
        public UIntPtr ProcessMemoryLimit;
        public UIntPtr JobMemoryLimit;
        public UIntPtr PeakProcessMemoryUsed;
        public UIntPtr PeakJobMemoryUsed;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateJobObjectW(IntPtr lpJobAttributes, string? lpName);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetInformationJobObject(
        IntPtr hJob, JobObjectInfoClass jobObjectInfoClass,
        ref JobObjectExtendedLimitInformation lpJobObjectInfo, uint cbJobObjectInfoLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr hObject);
}
