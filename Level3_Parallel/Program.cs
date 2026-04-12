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

var theradLocalResults= new Dictionary<string,CityStats>[threadCount];

var lineCounters = new long[threadCount];


Parallel.For(0, threadCount, threatIndex =>
{
    // Her thread için çalışan bölüm.

    // Başlangıç ve bitiş byte'larını belirleyelim.
    var startByte = chunkBoundaries[threatIndex];
    var endByte = chunkBoundaries[threatIndex + 1];
    // Okunacak byte sayısını hesaplayalım.
    var chunkLength = (int)(endByte - startByte);

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

});






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
