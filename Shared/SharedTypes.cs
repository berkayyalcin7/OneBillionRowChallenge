namespace Shared
{
    public static class GlobalConstants
    {
        public const string FilesDirectory = "C:\\Users\\BERKAY\\Desktop\\1BRC\\1BRCOutputs\\";
        public const string FilePath = "C:\\Users\\BERKAY\\Desktop\\1BRC\\1BRCOutputs\\measurements.txt";

        public const int ExpectedStationCount = 500;
    }

    public static class FileSelector
    {
        /// <summary>
        /// Lists all .txt files in the FilesDirectory and lets the user select one.
        /// </summary>
        /// <returns>Full path of the selected file, or null if no file is selected</returns>
        public static string? SelectFileFromDirectory()
        {
            var directory = new DirectoryInfo(GlobalConstants.FilesDirectory);

            if (!directory.Exists)
            {
                Console.WriteLine($"❌ Klasör bulunamadı: {GlobalConstants.FilesDirectory}");
                return null;
            }

            var files = directory.GetFiles("*.txt").OrderBy(f => f.Name).ToList();

            if (files.Count == 0)
            {
                Console.WriteLine($"⚠️  Klasörde .txt dosyası bulunamadı: {GlobalConstants.FilesDirectory}");
                return null;
            }

            Console.WriteLine($"📂 Klasörden bulunan dosyalar ({files.Count}):\n");
            for (int i = 0; i < files.Count; i++)
            {
                var fileSize = files[i].Length / 1024 / 1024; // MB
                Console.WriteLine($"  [{i + 1}] {files[i].Name} ({fileSize} MB)");
            }

            Console.WriteLine("\n📌 Dosya seçin (1-{0}) veya çıkış için (0): ", files.Count);

            if (int.TryParse(Console.ReadLine(), out int choice) && choice > 0 && choice <= files.Count)
            {
                var selectedFile = files[choice - 1].FullName;
                Console.WriteLine($"✅ Seçilen dosya: {selectedFile}\n");
                return selectedFile;
            }

            Console.WriteLine("❌ Geçersiz seçim.");
            return null;
        }
    }

    public static class ResultLogger
    {
        private const string ResultFileName = "results.log";

        /// <summary>
        /// Appends the 1BRC result to a log file in the solution directory.
        /// </summary>
        /// <param name="projectName">Name of the project (e.g., "Level01_Naive")</param>
        /// <param name="output">The 1BRC formatted output string</param>
        /// <param name="elapsed">Time elapsed for processing</param>
        /// <param name="rowCount">Number of rows processed</param>
        /// <param name="stationCount">Number of unique stations found</param>
        public static void SaveResult(
            string projectName,
            string output,
            TimeSpan elapsed,
            long rowCount,
            int stationCount)
        {
            try
            {
                var solutionDirectory = GlobalConstants.FilesDirectory;
                var filePath = Path.Combine(solutionDirectory, ResultFileName);

                // Collect memory statistics -> Ne kadarlık bellek kullanıldı, GC koleksiyon sayıları
                var workingSetMB = Environment.WorkingSet / 1024 / 1024;
                var gcMemoryMB = GC.GetTotalMemory(false) / 1024 / 1024;
                var gen0Collections = GC.CollectionCount(0);
                var gen1Collections = GC.CollectionCount(1);
                var gen2Collections = GC.CollectionCount(2);

                // Calculate throughput -> Ne kadar hızlı işlendi (satır/saniye ve MB/saniye)
                var throughputMBps = rowCount > 0 ? (rowCount * 25.0 / 1024 / 1024) / elapsed.TotalSeconds : 0; // Assuming ~25 bytes per row
                var rowsPerSecond = rowCount / elapsed.TotalSeconds;

                // Çıktıyı log formatında hazırla
                var logEntry = $"""
                ================================================================================
                [{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {projectName}
                ================================================================================
                Performance:
                  Rows:               {rowCount:N0}
                  Stations:           {stationCount}
                  Elapsed:            {elapsed}
                  Throughput:         {rowsPerSecond:N0} rows/sec ({throughputMBps:F2} MB/sec)
                
                Memory:
                  Working Set:        {workingSetMB:N0} MB
                  GC Memory:          {gcMemoryMB:N0} MB
                  Gen0 Collections:   {gen0Collections}
                  Gen1 Collections:   {gen1Collections}
                  Gen2 Collections:   {gen2Collections}
                
                Processor:
                  CPU Cores:          {Environment.ProcessorCount}
                --------------------------------------------------------------------------------
                {output}



                """;

                // Log dosyasına ekle
                File.AppendAllText(filePath, logEntry, System.Text.Encoding.UTF8);
                Console.WriteLine($"\n📁 Sonuçlar kaydedildi: {filePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n⚠️ Sonuçlar kaydedilemedi: {ex.Message}");
            }
        }



        /// <summary>
        /// Formats results to 1BRC output format. 
        /// </summary>
        public static string FormatOutput<T>(IEnumerable<KeyValuePair<string, T>> sortedResults)
            where T : notnull
        {
            return "{" + string.Join(", ", sortedResults.Select(kvp => $"{kvp.Key}={kvp.Value}")) + "}";
        }

        /// <summary>
        /// Formats results with custom formatter.
        /// </summary>
        public static string FormatOutput<T>(
            IEnumerable<KeyValuePair<string, T>> sortedResults,
            Func<T, string> formatter)
        {
            return "{" + string.Join(", ", sortedResults.Select(kvp => $"{kvp.Key}={formatter(kvp.Value)}")) + "}";
        }
    }

    public class CityStats
    {
        public double Min { get; set; } = double.MaxValue; // Sebep : İlk değerin Min olarak atanması, ilk güncellemede Min ve Max'ın doğru şekilde güncellenmesini sağlar.

        public double Max { get; set; } = double.MinValue; // Sebep: İlk değerin Max olarak atanması, ilk güncellemede Min ve Max'ın doğru şekilde güncellenmesini sağlar.

        public double Sum { get; set; }

        public int Count { get; set; }

        public double Mean => Count > 0 ? Sum / Count : 0;

        public void Update(double value)
        {

            if (value < Min) Min = value;
            if (value > Max) Max = value;

            Sum += value;
            Count++;
        }

        public void Merge(CityStats other)
        {
            if (other.Min < Min) Min = other.Min;
            if (other.Max > Max) Max = other.Max;
            Sum += other.Sum;
            Count += other.Count;
        }

        public override string ToString() => $"Min={Min}, Max={Max}, Mean={Mean:F2}";
    }
}

