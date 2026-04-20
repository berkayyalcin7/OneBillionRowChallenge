using Shared;
using System.Diagnostics;
using System.Text;

Console.WriteLine("=== Level 3   Parallel Implementation ===");
Console.WriteLine($"Processor count: {Environment.ProcessorCount}");
Console.WriteLine();

// Select file from directory
var selectedFilePath = FileSelector.SelectFileFromDirectory();
if (selectedFilePath == null)
{
    return;
}
// Verify file exists
if (!File.Exists(selectedFilePath))
{
    Console.WriteLine($"ERROR: File not found at {selectedFilePath}");
    return;
}

// Force garbage collection before measurement for accurate timing
// Anlamı : Garbage Collector (GC) tarafından yönetilen bellek temizlenir ve kullanılmayan nesneler bellekten atılır.
// Bu, bellek kullanımını optimize eder ve performansı artırır. GC.Collect() çağrısı, GC'nin hemen çalışmasını sağlar ve kullanılmayan nesneleri temizler.
// GC.WaitForPendingFinalizers() ise, GC'nin temizleme işlemi sırasında finalizer'ların tamamlanmasını bekler.
// Bu, bellek sızıntılarını önlemeye yardımcı olur ve uygulamanın daha stabil çalışmasını sağlar.
// Son olarak, ikinci bir GC.Collect() çağrısı, finalizer'lar tarafından oluşturulan yeni nesnelerin de temizlenmesini sağlar.
GC.Collect();
GC.WaitForPendingFinalizers();
GC.Collect();

var stopwatch = Stopwatch.StartNew();

var threadCount = Environment.ProcessorCount;   

// Her thread dosya içerisinden kendi bölümünü alması gerekiyor.
// Bu nedenle, dosya boyutunu ve thread sayısını göz önünde bulundurarak her thread'in okuyacağı bölümü hesaplamamız gerekiyor.

// Burada okuma yaparken kaç satırdan ziyade kaç byte okunacağını hesaplamak daha doğru olabilir, çünkü satır uzunlukları değişken olabilir.
// Byte byte okuduktan sonra satır sonu karakterlerini kontrol ederek satırları ayırabiliriz. Bu, özellikle büyük dosyalar için daha verimli olabilir.
// Her threadin kendi chunkını okuması gerekiyor.

// Okumaya başlayacağı yer
var chunkBoundaries = ComputeChunkBoundaries(selectedFilePath, new FileInfo(selectedFilePath).Length, threadCount);

// Her thread kendi chunkını okuyacak şekilde paralel işlemi başlatalım.

var threadLocalResults= new Dictionary<string,CityStats>[threadCount];

var lineCounters = new long[threadCount];


Parallel.For(0, threadCount, threadIndex =>
{
    // Her thread için çalışan bölüm.

    // Başlangıç ve bitiş byte'larını belirleyelim.
    var startByte = chunkBoundaries[threadIndex];
    var endByte = chunkBoundaries[threadIndex + 1];
    // Okunacak byte sayısını hesaplayalım.
    var chunkLength = (int)(endByte - startByte);

    // Bu bilgiyi struct olarak kullanabiliriz.
    var localStats = new Dictionary<string, CityStats>(GlobalConstants.ExpectedStationCount);

    var localLineCount = 0;
    // Dosyayı açalım ve sadece kendi chunkını okuyacak şekilde konumlandıralım.
    var buffer = new byte[chunkLength];

    using (var stream = new FileStream(selectedFilePath,
        FileMode.Open,
        FileAccess.Read,
        FileShare.Read))
    {
        stream.Seek(startByte, SeekOrigin.Begin);
        stream.ReadExactly(buffer, 0, chunkLength);
    }

    // Buffer'ı string'e dönüştürelim ve satırları ayıralım.
    var chunkText = Encoding.UTF8.GetString(buffer);

    var lineStart = 0;

    for(var i = 0; i < chunkText.Length; i++)
    {
        if(chunkText[i] == '\n')
        {
            // Satır sonu karakteri bulunduğunda, satırın başlangıç ve bitiş indekslerini belirleyelim.
            var lineEnd = i;

            // Satır sonu karakterlerini temizleyelim. Windows satır sonu \r\n olduğu için, \n karakterinden önce \r karakteri olabilir.
            if (lineEnd > lineStart && chunkText[lineEnd-1] == '\r')
            {
                lineEnd--;
            }

            if(lineEnd > lineStart)
            {
                // ProcessLine 
                ProcessLine(chunkText.AsSpan(lineStart, lineEnd - lineStart), localStats);
                localLineCount++;
            }
            // Bir sonraki satırın başlangıcı, şu anki satır sonunun hemen sonrasıdır.
            lineStart = i + 1;
        }
    }

    threadLocalResults[threadIndex]= localStats;
    lineCounters[threadIndex] = localLineCount;

});

// MERGE DICTIONARIES

var finalResults = new Dictionary<string, CityStats>(GlobalConstants.ExpectedStationCount);

// Her thread'in kendi localResults sözlüğü var. Bu sözlükleri tek bir finalResults sözlüğünde birleştirmemiz gerekiyor.
foreach (var localDict in threadLocalResults)
{
    // Eğer localDict null ise, bu thread'in herhangi bir veri işlemediği anlamına gelir. Bu durumda, bu thread'in sonuçlarını atlayabiliriz.
    if (localDict == null)
    {
        continue;
    }

    // Key value çiftlerini tek tek finalResults sözlüğüne ekleyelim. Eğer aynı anahtar zaten varsa, CityStats nesnelerini birleştirelim.
    foreach (var (stationName,stats) in localDict)
    {
        if(!finalResults.TryGetValue(stationName, out var existingStats))
        {
            existingStats = new CityStats();
            finalResults.Add(stationName, existingStats);
        }
        // Merge işlemi, mevcut istatistiklerle yeni istatistikleri birleştirir. Örneğin, toplam sıcaklık, maksimum sıcaklık ve ölçüm sayısı gibi değerleri günceller.
        existingStats.Merge(stats);
    }
}

var totalLines = lineCounters.Sum();

stopwatch.Stop();

static void ProcessLine(ReadOnlySpan<char> line, Dictionary<string, CityStats> localStats)
{
    var separator = line.IndexOf(';');
    if (separator < 0 || separator >= line.Length - 1)
        return;

    // Allocation var, çünkü line[..separator] ve line[(separator + 1)..] ifadeleri yeni string'ler oluşturur.
    // Ancak, bu işlemi yaparken ReadOnlySpan<char> kullanarak gereksiz string oluşturmayı önleyebiliriz.
    var stationName = line[..separator].ToString();

    // Temperature değerini double'a dönüştürelim.
    // Burada allocation yoktur. Çünkü double.Parse, ReadOnlySpan<char> alabilir ve doğrudan bu span üzerinden parse işlemi yapabilir.
    var temperature = double.Parse(line[(separator + 1)..]);


    if (!localStats.TryGetValue(stationName, out var stat))
    {
        stat = new CityStats();
        localStats.Add(stationName, stat);
    }

    stat.Update(temperature);
}

static long[] ComputeChunkBoundaries(string fileName, long fileSize, int threadCount)
{
    // 
    var boundaries = new long[threadCount + 1];
    int bomSize = 0;
    boundaries[threadCount] = fileSize;
    boundaries[0] = bomSize;

    var dataSize = fileSize - bomSize; // BOM (Byte Order Mark) varsa, bu kısmı atlamak için başlangıç noktasını 0'dan başlatıyoruz.
    var chunkSize = dataSize / threadCount;

    using var stream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read);
    // 1 den başlayarak threadCount-1'e kadar olan her thread için chunk sınırlarını belirleyelim
    for (int i = 1; i < threadCount; i++)
    {
        var targetPosition = i * chunkSize;
        // Dosya içerisinde hedef pozisyona git
        stream.Seek(targetPosition, SeekOrigin.Begin);

        int b;
        // Satır sonu karakteri bulunana kadar byte byte okumaya devam et
        while ((b = stream.ReadByte()) != -1)
        {
            if (b == '\n') // Satır sonu karakteri bulunduğunda chunk sınırını belirle
            {
                boundaries[i] = stream.Position; // Bir sonraki thread'in başlayacağı pozisyonu kaydet
                break;
            }
        }
        // Eğer dosya sonuna ulaşıldıysa, chunk sınırını dosya boyutuna ayarla
        if (b == -1)
        {
            boundaries[i] = fileSize;
        }
    }

    return boundaries;
}



var sortedResults = finalResults.OrderBy(kvp => kvp.Key).ToList();

var output = ResultLogger.FormatOutput(sortedResults);
Console.WriteLine(output);

Console.WriteLine();
Console.WriteLine($"Processed {totalLines:N0} rows using {threadCount} threads");
Console.WriteLine($"Found {finalResults.Count} unique stations");
Console.WriteLine($"Elapsed: {stopwatch.Elapsed}");

// Save results to file
ResultLogger.SaveResult(
    projectName: "Level03_Parallel",
    output: output,
    elapsed: stopwatch.Elapsed,
    rowCount: totalLines,
    stationCount: finalResults.Count);

// Per-thread statistics
Console.WriteLine();
Console.WriteLine("Per-Thread Statistics:");
for (var i = 0; i < threadCount; i++)
{
    var stationCount = threadLocalResults[i]?.Count ?? 0;
    Console.WriteLine($"  Thread {i}: {lineCounters[i]:N0} lines, {stationCount} stations");
}


