using OpenCvSharp;
using System.Collections.Concurrent;
using System.Diagnostics;
using Tesseract;

namespace FindTextFromVideo
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 3)
            {
                Console.WriteLine("Usage: FindTextFromVideo.exe \"<text to find>\" <video path> <output text file> [skip frames]");
                return;
            }

            string searchText = args[0].Trim('"'); // Remove quotes if present
            string videoPath = args[1];
            string outputTextFile = args[2];

            int skipFrames = 1; // Default to no skipping
            if (args.Length >= 4 && int.TryParse(args[3], out var skip))
            {
                skipFrames = Math.Max(1, skip); // Ensure skip is at least 1
            }

            if (string.IsNullOrEmpty(searchText))
            {
                Console.WriteLine("The text to find cannot be empty.");
                return;
            }

            if (!File.Exists(videoPath))
            {
                Console.WriteLine($"The video file '{videoPath}' does not exist.");
                return;
            }

            try
            {
                using (var videoCapture = new VideoCapture(videoPath))
                {
                    if (!videoCapture.IsOpened())
                    {
                        Console.WriteLine("Failed to open the video file.");
                        return;
                    }

                    int frameCount = (int)videoCapture.Get(VideoCaptureProperties.FrameCount);
                    double fps = videoCapture.Get(VideoCaptureProperties.Fps); // Frames per second
                    Console.WriteLine($"Video loaded. Frame count: {frameCount}, FPS: {fps}, Skip Frames: {skipFrames}");

                    var frames = new ConcurrentBag<(int FrameIndex, Mat Frame)>();
                    var results = new ConcurrentDictionary<int, string>();
                    object fileLock = new object();

                    // Initialize output file
                    using (var writer = new StreamWriter(outputTextFile))
                    {
                        writer.WriteLine("Frame Index\tTimestamp\tOCR Text");
                    }

                    // Timer for extraction
                    Stopwatch extractionStopwatch = Stopwatch.StartNew();

                    // Extract all frames with progress bar
                    Console.WriteLine("Extracting frames...");
                    for (int i = 0; i < frameCount; i += skipFrames)
                    {
                        Mat frame = new Mat();
                        videoCapture.Set(VideoCaptureProperties.PosFrames, i);
                        videoCapture.Read(frame);
                        if (frame.Empty()) break;

                        frames.Add((i, frame));

                        // Update progress bar for extraction
                        double extractionProgress = (double)(i + 1) / frameCount;
                        TimeSpan elapsedTime = extractionStopwatch.Elapsed;
                        TimeSpan estimatedTotalTime = TimeSpan.FromTicks((long)(elapsedTime.Ticks / extractionProgress));
                        TimeSpan remainingTime = estimatedTotalTime - elapsedTime;

                        Console.Write($"\rExtracting: [{new string('#', (int)(extractionProgress * 50))}{new string('-', 50 - (int)(extractionProgress * 50))}] {extractionProgress * 100:F2}% | Elapsed: {elapsedTime:hh\\:mm\\:ss} | Remaining: {remainingTime:hh\\:mm\\:ss}");
                    }

                    Console.WriteLine($"\nExtraction complete. Extracted {frames.Count} frames.");

                    // Timer for processing
                    Stopwatch processingStopwatch = Stopwatch.StartNew();

                    int detectedCount = 0;
                    int processedFrames = 0;

                    // Process frames in parallel with progress bar
                    Console.WriteLine("Processing frames...");
                    Parallel.ForEach(frames, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount - 1 }, frameData =>
                    {
                        int frameIndex = frameData.FrameIndex;
                        Mat frame = frameData.Frame;

                        // Convert the frame to grayscale for better OCR performance
                        Mat grayFrame = new Mat();
                        Cv2.CvtColor(frame, grayFrame, ColorConversionCodes.BGR2GRAY);

                        // Use Tesseract for OCR
                        using (var tesseractEngine = new TesseractEngine("./tessdata", "eng", EngineMode.Default))
                        {
                            using (var bitmap = OpenCvSharp.Extensions.BitmapConverter.ToBitmap(grayFrame))
                            {
                                using (var ms = new MemoryStream())
                                {
                                    bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                                    ms.Seek(0, SeekOrigin.Begin);

                                    using (var pix = Pix.LoadFromMemory(ms.ToArray()))
                                    {
                                        using (var page = tesseractEngine.Process(pix))
                                        {
                                            string ocrText = page.GetText();

                                            // Calculate timestamp
                                            double seconds = frameIndex / fps;
                                            TimeSpan timestamp = TimeSpan.FromSeconds(seconds);

                                            // Add result to dictionary
                                            results[frameIndex] = $"{frameIndex}\t{timestamp:hh\\:mm\\:ss\\.fff}\t{ocrText.Replace("\n", " ")}";

                                            // Check if the search text is found
                                            if (ocrText.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                                            {
                                                Console.WriteLine($"\nText found in frame {frameIndex} at timestamp {timestamp:hh\\:mm\\:ss\\.fff}");
                                                Interlocked.Increment(ref detectedCount);
                                            }

                                            // Periodic file update
                                            if (processedFrames % 10 == 0) // Update file every 10 frames
                                            {
                                                lock (fileLock)
                                                {
                                                    using (var writer = new StreamWriter(outputTextFile, append: true))
                                                    {
                                                        foreach (var key in results.Keys.OrderBy(k => k))
                                                        {
                                                            if (results.TryRemove(key, out var line))
                                                            {
                                                                writer.WriteLine(line);
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        grayFrame.Dispose();

                        // Update progress bar for processing
                        Interlocked.Increment(ref processedFrames);
                        double processingProgress = (double)processedFrames / frames.Count;
                        TimeSpan processingElapsed = processingStopwatch.Elapsed;
                        TimeSpan processingEstimatedTotalTime = TimeSpan.FromTicks((long)(processingElapsed.Ticks / processingProgress));
                        TimeSpan processingRemainingTime = processingEstimatedTotalTime - processingElapsed;

                        Console.Write($"\rProcessing: [{new string('#', (int)(processingProgress * 50))}{new string('-', 50 - (int)(processingProgress * 50))}] {processingProgress * 100:F2}% | Elapsed: {processingElapsed:hh\\:mm\\:ss} | Remaining: {processingRemainingTime:hh\\:mm\\:ss}");
                    });

                    // Write remaining results to file
                    Console.WriteLine("\nFinalizing output...");
                    lock (fileLock)
                    {
                        using (var writer = new StreamWriter(outputTextFile, append: true))
                        {
                            foreach (var key in results.Keys.OrderBy(k => k))
                            {
                                if (results.TryRemove(key, out var line))
                                {
                                    writer.WriteLine(line);
                                }
                            }
                        }
                    }

                    processingStopwatch.Stop();

                    Console.WriteLine("\nProcessing complete.");
                    Console.WriteLine($"Total frames processed: {frames.Count}");
                    Console.WriteLine($"Total frames with text found: {detectedCount}");
                    Console.WriteLine($"Elapsed time: {processingStopwatch.Elapsed:hh\\:mm\\:ss}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nAn error occurred: {ex.Message}");
            }
        }
    }
}
