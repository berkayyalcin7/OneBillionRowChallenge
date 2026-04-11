using Shared;
using System.Diagnostics;
using System.Text;

// Stack : Veri boyutu küçük ve ömrü kısa olan veriler için kullanılır. (örneğin int, double gibi değer tipleri)

// Heap : Veri boyutu büyük ve ömrü uzun olan veriler için kullanılır. (örneğin string, class gibi referans tipleri)

// Stringler immutable (değiştirilemez) olduğu için her değişiklikte yeni bir string oluşturulur ve eski string heap'te kalır.
string name = "Geraint";

string newName = name; // newName ve name aynı stringi gösterir. Heap'te tek bir "Geraint" stringi vardır.

name = "Geraint Thomas"; // name değişkeni yeni bir string oluşturur. Heap'te "Geraint Thomas" adında yeni bir string oluşur. newName hala "Geraint" stringini gösterir.

// GC (garbage collector) heap'te kullanılmayan nesneleri temizler. Ancak GC'nin ne zaman çalışacağı belirsizdir.
// Bu nedenle büyük miktarda bellek tahsisi yaparken GC'yi manuel olarak tetiklemek performans ölçümlerinde daha doğru sonuçlar verebilir.



Console.WriteLine("=== Level 1: Naive (LINQ) Implementation ===");
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

// 13 MB büyüklüğünde bir dosyayı UTF-8 kodlamasıyla satır satır okuyarak işle. (UTF-8 Olduğu için 13*2)
// 4 karakterlik bir string C# da yaklaşık 20 byte yer kaplar. Satır başına ortalama 25 byte olduğunu varsayarsak,
// 13 MB dosyada yaklaşık 500.000 satır olabilir.
var lines = File.ReadAllLines(selectedFilePath, Encoding.UTF8);


// Her satırda dolaşıp ; e göre bölüp istasyon adını alma.
var results = lines.Select(line =>
{
    // ReadOnlySpan<char>
    ReadOnlySpan<char> parts = line.AsSpan(); // 2 elemanlı bir array: [0] = istasyon adı, [1] = sıcaklık değeri. Yeni bir allocasyon yapılır.
    var partsIndex = parts.IndexOf(';'); // ; karakterinin indexini bulmak için yeni bir allocasyon yapılır. 1 milyon satır için 1 milyon allocasyon.
    var station = parts.Slice(0, partsIndex); // İstasyon adını almak için yeni bir allocasyon yapılır. 1 milyon satır için 1 milyon allocasyon.
    var temperature = parts.Slice(partsIndex + 1); // Sıcaklık değerini almak için yeni bir allocasyon yapılır. 1 milyon satır için 1 milyon allocasyon.
    
    return new // 1 milyon tane yeni nesne. Heap'e gidecek.
    {
        Station = station.ToString(), // String
        Temperature = double.Parse(temperature), // double
    };
}).GroupBy(x => x.Station) // Yeni bir allocation. Bellek tahsisi yapılır. 413 satır üretti
    .Select(g => new // Yeni bir nesne 413 tane
    {
        Station = g.Key,
        Min = g.Min(x => x.Temperature), // 413x kere çalışır
        Avg= g.Average(x => x.Temperature), // 413x kere çalışır
        Max = g.Max(x => x.Temperature) // 413x kere çalışır
    })
    // İstasyon adına göre sıralama yapma.
    .OrderBy(x => x.Station) // 413 satırı sıralamak için yeni bir allocation. Bellek tahsisi yapılır.
    .ToList(); // Sonuçları listeye atmak için yeni bir allocation. Bellek tahsisi yapılır. 413 nesne içeren bir liste oluşur.

stopwatch.Stop();

// F1 : Ondalık kısmı 1 basamak göstermek için kullanılır. Örneğin 23.456 -> 23.5
var output = "{" + string.Join(", ",
    results.Select(r => $"{r.Station}={r.Min:F1}/{r.Avg:F1}/{r.Max:F1}")) +
    "}";

Console.WriteLine(output);

Console.WriteLine();
Console.WriteLine($"Processed {lines.Length:N0} rows");
Console.WriteLine($"Found {results.Count} unique stations");
Console.WriteLine($"Elapsed: {stopwatch.Elapsed}");

// Save results to file
ResultLogger.SaveResult(
    projectName: "Level01_Naive",
    output: output,
    elapsed: stopwatch.Elapsed,
    rowCount: lines.Length,
    stationCount: results.Count);