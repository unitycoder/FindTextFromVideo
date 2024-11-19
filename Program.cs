using OpenCvSharp;
using System.Diagnostics;
using Tesseract;

namespace FindTextFromVideo
{

    class Program
    {
        static void Main(string[] args)
        {
            // Validate input arguments
            if (args.Length < 3)
            {
                Console.WriteLine("Usage: FindTextFromVideo.exe \"<text to find>\" <video path> <output text file>");
                return;
            }

            string searchText = args[0].Trim('"'); // Remove quotes if present
            string videoPath = args[1];
            string outputTextFile = args[2];

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
                    Console.WriteLine($"Video loaded. Frame count: {frameCount}, FPS: {fps}");

                    var frame = new Mat();
                    int frameIndex = 0;
                    int detectedCount = 0;

                    // Timer to track progress
                    Stopwatch stopwatch = Stopwatch.StartNew();

                    // Initialize Tesseract
                    using (var tesseractEngine = new TesseractEngine("./tessdata", "eng", EngineMode.Default))
                    {
                        using (var writer = new StreamWriter(outputTextFile))
                        {
                            writer.WriteLine("Frame Index\tTimestamp\tOCR Text");
                            while (true)
                            {
                                videoCapture.Read(frame);

                                if (frame.Empty())
                                {
                                    break; // End of video
                                }

                                // Convert the frame to grayscale for better OCR performance
                                Mat grayFrame = new Mat();
                                Cv2.CvtColor(frame, grayFrame, ColorConversionCodes.BGR2GRAY);

                                // Convert frame to a bitmap for Tesseract
                                using (var bitmap = OpenCvSharp.Extensions.BitmapConverter.ToBitmap(grayFrame))
                                {
                                    using (var ms = new MemoryStream())
                                    {
                                        // Save the Bitmap to MemoryStream as PNG
                                        bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                                        ms.Seek(0, SeekOrigin.Begin);

                                        // Load Pix from memory stream
                                        using (var pix = Pix.LoadFromMemory(ms.ToArray()))
                                        {
                                            // Perform OCR on the frame
                                            using (var page = tesseractEngine.Process(pix))
                                            {
                                                string ocrText = page.GetText();

                                                // Calculate timestamp
                                                double seconds = frameIndex / fps;
                                                TimeSpan timestamp = TimeSpan.FromSeconds(seconds);

                                                // Write OCR text to the output file
                                                writer.WriteLine($"{frameIndex}\t{timestamp:hh\\:mm\\:ss\\.fff}\t{ocrText.Replace("\n", " ")}");

                                                // Check if the search text is found
                                                if (ocrText.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                                                {
                                                    Console.WriteLine($"\nText found in frame {frameIndex} at timestamp {timestamp:hh\\:mm\\:ss\\.fff}");
                                                    detectedCount++;
                                                }
                                            }
                                        }
                                    }
                                }

                                grayFrame.Dispose();

                                // Progress bar and time calculation
                                frameIndex++;
                                double progress = (double)frameIndex / frameCount;
                                TimeSpan elapsedTime = stopwatch.Elapsed;
                                TimeSpan estimatedTotalTime = TimeSpan.FromTicks((long)(elapsedTime.Ticks / progress));
                                TimeSpan remainingTime = estimatedTotalTime - elapsedTime;

                                Console.Write($"\rProcessing: [{new string('#', (int)(progress * 50))}{new string('-', 50 - (int)(progress * 50))}] {progress * 100:F2}% | Elapsed: {elapsedTime:hh\\:mm\\:ss} | Remaining: {remainingTime:hh\\:mm\\:ss}");
                            }
                        }
                    }

                    Console.WriteLine("\nProcessing complete.");
                    Console.WriteLine($"Total frames processed: {frameCount}");
                    Console.WriteLine($"Total frames with text found: {detectedCount}");
                    Console.WriteLine($"OCR text saved to: {outputTextFile}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nAn error occurred: {ex.Message}");
            }
        }
    }

} // namespace
