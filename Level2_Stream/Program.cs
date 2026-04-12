//// İstanbul;34.2
//string line = "İstanbul;34.2";
//var parts = line.Split(';');//allocasyon yapılır. 1 milyon satır için 1 milyon allocasyon.


//string station = parts[0]; // referans kopyalaması.
//string temperature = parts[1];

//ReadOnlySpan<char> lineSpan = line.AsSpan(); // Stack üzerinde çalışır. Heap'e giden bir veri yoktur.


//// Span tam olarak stack üzerinde çalışır. Heap'e giden bir veri yoktur.
//var ms = new MySpan
//{
//    OriginalString = line,
//    First = 0,
//    Last = 3
//};


//class MySpan
//{
//    public string OriginalString { get;  set; }

//    public int First {  get; set; }

//    public int Last { get; set; }
//}
using Shared;
using System.Diagnostics;
using System.Text;

Console.WriteLine("=== Level 2: Streaming Implementation ===");
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
GC.Collect();
GC.WaitForPendingFinalizers();
GC.Collect();

var stopwatch = Stopwatch.StartNew();

// Kapasite belirtmek bellek tahsisini optimize eder. Beklenen istasyon sayısına göre bir kapasite belirleyelim.
//Buffer size değiştirerek performansı etkileyebiliriz.
//Varsayılan buffer size genellikle 4KB'dir,
//ancak bu değeri artırarak daha büyük bloklar halinde okuyabiliriz.
//Bu, disk I/O işlemlerinin sayısını azaltarak performansı artırabilir.
//Ancak, çok büyük bir buffer size bellek kullanımını artırabilir, bu yüzden dikkatli bir şekilde seçilmelidir.
//Örneğin, 64KB veya 128KB gibi bir buffer size deneyebiliriz.
int bufferSize = 64 * 1024; // 64KB

// OS Page Size genellikle 4KB'dir, bu yüzden buffer size'ı page size'ın katları olarak seçmek performansı artırabilir.

// NTFS Dosya sisteminde standart bir Cluster boyutu 4KB'dir, bu yüzden buffer size'ı cluster boyutunun katları olarak seçmek performansı artırabilir.

// Dosyayı okuma işlemi sırasında bellekte büyük bir veri yapısı oluşturmadan, satır satır okuyarak işlemi gerçekleştireceğiz.
// detectEncoding parametresi, dosyanın karakter kodlamasını otomatik olarak algılamaya çalışır. Bu, farklı kodlama türlerine sahip dosyaları doğru şekilde okuyabilmemizi sağlar.
using var reader = new StreamReader(selectedFilePath, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: bufferSize);
string line;


var stations = new Dictionary<string, CityStats>(GlobalConstants.ExpectedStationCount);
int lineCounter = 0;   
// Bufferdan satır satır okuyarak işlemi gerçekleştireceğiz. Bu sayede bellekte büyük bir veri yapısı oluşturmadan dosyayı işleyebiliriz.
while ((line = reader.ReadLine()) != null)
{
    lineCounter++;
    // Bilgileri almamız lazım.
    var seperator = line.IndexOf(';');

    var stationName = line[..seperator]; // Allocation. 
    
    var temperature = double.Parse(line.AsSpan(seperator + 1)); // Allocation yok. Parse işlemi doğrudan ReadOnlySpan<char> üzerinde çalışır.

    // Dictionary arka planda bir hash tablosu kullanır. Bu nedenle, arama işlemi ortalama O(1) zaman karmaşıklığına sahiptir.
    // Ancak, hash çakışmaları durumunda bu karmaşıklık O(n) olabilir, ancak iyi bir hash fonksiyonu ve uygun kapasite ile bu durum minimize edilir.
    if (!stations.TryGetValue(stationName,out var stat))
    {
        stat = new CityStats(); // allocation. 413 kere yapılır.

        stations.Add(stationName, stat);
    }

    // Verileri güncelleyelim.
    stat.Update(temperature);
} 

stopwatch.Stop();


// OUTPUT 
var sortedResult = stations.OrderBy(kvp=>kvp.Key).ToList();

var output = ResultLogger.FormatOutput(sortedResult);

Console.WriteLine(output);

Console.WriteLine();

Console.WriteLine($"Processed {lineCounter:N0} rows.");
Console.WriteLine($"Found {stations.Count} unique stations.");
Console.WriteLine($"Elapsed Time: {stopwatch.Elapsed}");

ResultLogger.SaveResult("Level2_Stream", 
    output, 
    stopwatch.Elapsed, 
    lineCounter, 
    stations.Count);

Console.WriteLine("✅ Result saved to log file.");
