
using Shared;
using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Text;
using System.Text.Unicode;

Console.WriteLine("=== Level 4   Shared Memory ===");
Console.WriteLine($"Processor count: {Environment.ProcessorCount}");
Console.WriteLine();

// Select file from directory
var selectedFilePath = FileSelector.SelectFileFromDirectory();
var fileSize = new FileInfo(selectedFilePath).Length;
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

GC.Collect();
GC.WaitForPendingFinalizers();
GC.Collect();

var stopwatch = Stopwatch.StartNew();

var threadCount = Environment.ProcessorCount;


// Her thread kendi chunkını okuyacak şekilde paralel işlemi başlatalım.
// Sadece dictionary'ye ekleme için Name değerine ihtiyacımız olacak. 413 tane string oluşturacağız.
// Collisionları azaltacak hash algoritması kullanacağız
var threadLocalResults = new Dictionary<int, (string Name, CityStatsStruct Stats)>[threadCount];

var lineCounters = new long[threadCount];

//MemoryMappedFiles : Büyük dosyaları bellek haritalama yöntemiyle işlemek, performansı artırabilir çünkü dosya içeriği doğrudan bellek üzerinden erişilebilir hale gelir.
//Bu, özellikle büyük dosyalar için disk I/O'yu azaltarak hız kazanımı sağlar.
//Ancak, MemoryMappedFiles kullanırken dikkatli olunmalı ve uygun şekilde yönetilmelidir, çünkü yanlış kullanım bellek sızıntılarına veya performans sorunlarına yol açabilir.

// MemoryMappedFile oluşturuyoruz. Bu, dosyanın tamamını veya belirli bir bölümünü bellek haritasına dönüştürür.
using var mmf = MemoryMappedFile.CreateFromFile(selectedFilePath, FileMode.Open, null, 0, MemoryMappedFileAccess.Read);
// MemoryMappedFile'dan bir view accessor oluşturuyoruz. Bu accessor, dosyanın belirli bir bölümüne erişim sağlar.
using var accessor = mmf.CreateViewAccessor(0, fileSize, MemoryMappedFileAccess.Read);

unsafe
{
    byte* basePtr = null;

    accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref basePtr);

    try
    {
        // Bolca bound check ve güvenlik kontrolleri yaparak pointer ile dosya içeriğine erişebiliriz.
        long dataStart = 0;
        if(fileSize >=3 && basePtr[0] == 0xEF && basePtr[1] == 0xBB && basePtr[2] == 0xBF)
        {
            // UTF-8 BOM (Byte Order Mark) varsa, verinin başlangıç noktasını 3 byte ileri alıyoruz.
            dataStart = 3;
        }

        var dataSize= fileSize- dataStart;
        var chunkSize = dataSize/ threadCount;

        Parallel.For(0, threadCount, threadIndex =>
        {
            // Calculate chunk boundraies (relative to data start , after BOM)
            var startPos = dataStart + (threadIndex * chunkSize);

            // Mantık : Her thread kendi chunkını okuyacak şekilde konumlanacak. Son thread ise dosyanın sonuna kadar okuyacak şekilde ayarlan
            var endPos = (threadIndex == threadCount -1) ? fileSize : dataStart+((threadIndex+1)*chunkSize);

            // Her thread'in kendi chunkını okuyacak şekilde konumlanması gerekiyor.
            // Ancak, chunk sınırları satır ortasında olabilir.
            // Bu nedenle, her thread'in başlangıç ve bitiş noktalarını satır sonu karakterlerine göre ayarlamak gerekebilir.
            if (startPos > endPos)
            {
                // Bu durum, dosya boyutunun thread sayısına tam bölünmemesi durumunda ortaya çıkabilir. Son thread'in bitiş noktası dosya sonu olduğu için bu kontrolü yapıyoruz.
                while (startPos < fileSize && basePtr[startPos] != '\n')
                {
                    // Satır sonu karakteri bulunana kadar startPos'u artırarak satırın sonuna gelmeye çalışıyoruz.
                    startPos++;
                }
            }

            // Diğer Boundrie kontrolü . Aynı Threadin bitiş pozisyonunuda satır sonuna göre ayarlayalım.
            if (endPos < fileSize && threadIndex < threadCount-1)
            {
                // Satır sonu karakteri bulunana kadar endPos'u artırarak satırın sonuna gelmeye çalışıyoruz.
                while (endPos < fileSize && basePtr[endPos-1] != '\n')
                {
                    endPos++;
                }
            }

            // Thread local dictionary using hash codes as keys
            var localStats = new Dictionary<int, (string Name, CityStatsStruct Stats)>(GlobalConstants.ExpectedStationCount);
            long localLineCount = 0;
            var pos =startPos;

            while(pos< endPos)
            {
                //  Find Semicolon
                var semicolonPos = pos;
                // boundrie kontrolü yaparak ; karakterini arıyoruz. Satır sonuna gelmeden önce ; karakteri bulunmazsa, bu durum hatalı bir format olduğunu gösterebilir.
                while (semicolonPos < endPos && basePtr[semicolonPos] != ';')
                {
                    semicolonPos++;
                }

                if(semicolonPos >= endPos)
                {
                    break;
                }

                var newlinePos= semicolonPos +1;
                while (newlinePos < endPos && basePtr[newlinePos] != '\n')
                {
                    newlinePos++;
                }

                // Semicolon ve newline karakterlerinin pozisyonlarını kullanarak Name ve Stats bölümlerini ayırıyoruz.
                if (newlinePos>=endPos && threadIndex < threadCount-1)
                {
                    break;
                }
                var nameSpan = new ReadOnlySpan<byte>(basePtr + pos, (int)(semicolonPos - pos)); // Name bölümü
                var hash = ComputeHash(nameSpan); // Name bölümünün hash'ini hesaplıyoruz. Bu, dictionary'ye eklerken collisionları azaltmaya yardımcı olabilir.


                var tempLen = (int)(newlinePos - semicolonPos - 1); // Stats bölümünün uzunluğunu hesaplıyoruz. Bu, newline karakterine kadar olan kısmı temsil eder.

                if (tempLen > 0 && newlinePos >0 && basePtr[newlinePos-1] == '\r') // Eğer temp değerinin sonunda \r karakteri varsa, bu karakteri temp değerinden çıkartmak için tempLen'i 1 azaltıyoruz.
                {
                    tempLen--; // Eğer temp değerinin sonunda \r karakteri varsa, bu karakteri temp değerinden çıkartıyoruz.
                }

                var tempSpan = new ReadOnlySpan<byte>(basePtr + semicolonPos + 1, tempLen); // Stats bölümü

                var temperature = ParseTemperature(tempSpan); // Stats bölümünden sıcaklık değerini parse ediyoruz.

                // Update Statistics
                if(localStats.TryGetValue(hash, out var existingStats))
                {
                    // Eğer hash zaten dictionary'de varsa, mevcut istatistikleri güncelliyoruz.
                    existingStats.Stats.Update(temperature);
                    localStats[hash] = existingStats;
                }
                else
                {
                    // Eğer hash dictionary'de yoksa, yeni bir istatistik oluşturup ekliyoruz.
                    var name =Encoding.UTF8.GetString(nameSpan);
                    var stats= CityStatsStruct.Create();
                    localStats[hash] = (name, stats);
                }

                localLineCount++;
                pos = newlinePos + 1; // Bir sonraki satırın başlangıç pozisyonuna geçiyoruz.

            }

            threadLocalResults[threadIndex] = localStats;
            lineCounters[threadIndex] = localLineCount;

        });

    }
    finally
    {
        // Mutlaka kapatılması gereken bir işlem olduğu için finally bloğunda bırakıyoruz.
        accessor.SafeMemoryMappedViewHandle.ReleasePointer();
    }
}

var finalResults = new Dictionary<string, CityStatsStruct>(GlobalConstants.ExpectedStationCount);

foreach (var localStats in threadLocalResults)
{
    if (localStats == null) continue;

    foreach (var (_, (name, stats)) in localStats)
    {
        if (finalResults.TryGetValue(name, out var existingStats))
        {
            existingStats.Merge(in stats);
            finalResults[name] = existingStats;
        }
        else
        {
            finalResults[name] = stats;
        }
    }
}

var totalLines = lineCounters.Sum();

stopwatch.Stop();

// =============================================================================
// OUTPUT: Format results as required by 1BRC
// =============================================================================

var sortedResults = finalResults.OrderBy(kvp => kvp.Key).ToList();

var output = ResultLogger.FormatOutput(sortedResults, s => $"{s.Min:F1}/{s.Mean:F1}/{s.Max:F1}");
Console.WriteLine(output);

Console.WriteLine();
Console.WriteLine($"Processed {totalLines:N0} rows using {threadCount} threads");
Console.WriteLine($"Found {finalResults.Count} unique stations");
Console.WriteLine($"Elapsed: {stopwatch.Elapsed}");

// Save results to file
ResultLogger.SaveResult(
    projectName: "Level04_SharedMemory",
    output: output,
    elapsed: stopwatch.Elapsed,
    rowCount: totalLines,
    stationCount: finalResults.Count);

// Memory info
Console.WriteLine();
Console.WriteLine("Memory Statistics:");
Console.WriteLine($"  Working Set: {Environment.WorkingSet / 1024 / 1024:N0} MB");
Console.WriteLine($"  GC Total Memory: {GC.GetTotalMemory(false) / 1024 / 1024:N0} MB");
Console.WriteLine($"  Gen0 Collections: {GC.CollectionCount(0)}");
Console.WriteLine($"  Gen1 Collections: {GC.CollectionCount(1)}");
Console.WriteLine($"  Gen2 Collections: {GC.CollectionCount(2)}");


// double.Parse yerine kendi ParseTemperature fonksiyonumuzu yazalım.
// Bu, string'i double'a çevirmek için daha hızlı olabilir çünkü gereksiz kontrolleri atlayabiliriz ve doğrudan byte'lar üzerinde çalışabiliriz.
// Ancak, bu fonksiyonun doğru ve güvenli bir şekilde yazılması önemlidir, çünkü hatalı bir implementasyon yanlış sonuçlara veya uygulama çökmelerine neden olabilir.
static double ParseTemperature(ReadOnlySpan<byte> span)
{
    // ASCII kodlarında '0' karakterinin değeri 48'dir. Bu nedenle, bir karakterin sayısal değerini elde etmek için karakterin ASCII değerinden '0' karakterinin ASCII değerini çıkarırız.
    // Örneğin, '5' karakteri ASCII kodunda 53'e karşılık gelir, bu yüzden '5' - '0' = 53 - 48 = 5 sonucunu verir.
    if(span.Length> 0 && span[^1] == '\r')
    {
        span = span[..^1];
    }
    var negative = false;
    var index = 0;

    if (span[0] == '-')
    {
        negative = true;
        index=1;
    }

    double result = 0;
    var decimalFound = false;
    var decimalPlace = 0.1;

    while (index < span.Length)
    {
        var currentByte = span[index];

      

        if (currentByte == '.')
        {
            // Ondalık nokta bulunduğunda, bundan sonraki rakamların ondalık kısmı olduğunu belirtmek için decimalFound bayrağını true yapıyoruz.
            decimalFound = true;
        }
        // else if : Birden fazla koşulu kontrol etmek için kullanılır.
        // İlk koşul sağlanmazsa, ikinci koşul kontrol edilir. Bu, birden fazla durumun kontrol edilmesi gerektiğinde kodun daha okunabilir ve düzenli olmasını sağlar.
        else if (currentByte >= '0' && currentByte <= '9')
        {
            // '0' karakterinin ASCII değeri 48 olduğu için, bir karakterin sayısal değerini elde etmek için karakterin ASCII değerinden '0' karakterinin ASCII değerini çıkarırız.
            var digit = currentByte - '0';
            if (decimalFound)
            {
                result += digit * decimalPlace;
                decimalPlace *= 0.1;
            }
            else
            {
                result = result * 10 + digit;
            }
        }
        else
        {
            throw new FormatException($"Invalid character '{(char)currentByte}' in temperature string.");
        }
        index++;
    }
    return negative ? -result : result;
}


// FNV -1a : Hash algoritması kullanarak stringlerin hash'lerini hesaplayarak dictionary'ye ekleyelim. Bu, collisionları azaltabilir ve performansı artırabilir.
// Günümüzde alternatif olarak : Non-cryptographic hash algoritmaları (örneğin, xxHash, CityHash) daha hızlı ve daha düşük collision oranlarına sahip olabilirler.
// Ancak, FNV-1a hala basit ve etkili bir seçenek olarak kullanılabilir.
static int ComputeHash(ReadOnlySpan<byte> span)
{

    // unchecked : C#'ta aritmetik işlemlerde taşma durumunda hata fırlatılmasını engellemek için kullanılır.
    // Bu, özellikle hash hesaplamalarında önemlidir çünkü hash değerleri genellikle taşabilir ve bu durumun performansı olumsuz etkilememesi istenir.
    unchecked
    {
        var hash = unchecked((int)2166136261);
        foreach (byte b in span)
        {
            hash ^= b;
            hash *= 16777619;
        }
        return hash;
    }
}

//xxHash ile Compute Hash yapmak için 
//static int ComputeHashWithXXHash(ReadOnlySpan<byte> span)
//{
//    // xxHash32 implementation (seed = 0)
//    unchecked
//    {
//        const uint PRIME1 = 2654435761u;
//        const uint PRIME2 = 2246822519u;
//        const uint PRIME3 = 3266489917u;
//        const uint PRIME4 = 668265263u;
//        const uint PRIME5 = 374761393u;

//        static uint Rotl(uint x, int r) => (x << r) | (x >> (32 - r));

//        var length = span.Length;
//        uint h;

//        int offset = 0;
//        if (length >= 16)
//        {
//            uint v1 = PRIME1 + PRIME2;
//            uint v2 = PRIME2;
//            uint v3 = 0u - PRIME1;
//            uint v4 = 0u - PRIME2;

//            int limit = length - 16;
//            while (offset <= limit)
//            {
//                uint read1 = (uint)(span[offset] | (span[offset + 1] << 8) | (span[offset + 2] << 16) | (span[offset + 3] << 24));
//                uint read2 = (uint)(span[offset + 4] | (span[offset + 5] << 8) | (span[offset + 6] << 16) | (span[offset + 7] << 24));
//                uint read3 = (uint)(span[offset + 8] | (span[offset + 9] << 8) | (span[offset + 10] << 16) | (span[offset + 11] << 24));
//                uint read4 = (uint)(span[offset + 12] | (span[offset + 13] << 8) | (span[offset + 14] << 16) | (span[offset + 15] << 24));

//                v1 = Rotl(v1 + read1 * PRIME2, 13) * PRIME1;
//                v2 = Rotl(v2 + read2 * PRIME2, 13) * PRIME1;
//                v3 = Rotl(v3 + read3 * PRIME2, 13) * PRIME1;
//                v4 = Rotl(v4 + read4 * PRIME2, 13) * PRIME1;

//                offset += 16;
//            }

//            h = Rotl(v1, 1) + Rotl(v2, 7) + Rotl(v3, 12) + Rotl(v4, 18);

//            v1 *= PRIME2; v1 = Rotl(v1, 13); v1 *= PRIME1; h ^= v1; h = h * PRIME1 + PRIME4;
//            v2 *= PRIME2; v2 = Rotl(v2, 13); v2 *= PRIME1; h ^= v2; h = h * PRIME1 + PRIME4;
//            v3 *= PRIME2; v3 = Rotl(v3, 13); v3 *= PRIME1; h ^= v3; h = h * PRIME1 + PRIME4;
//            v4 *= PRIME2; v4 = Rotl(v4, 13); v4 *= PRIME1; h ^= v4; h = h * PRIME1 + PRIME4;
//        }
//        else
//        {
//            h = PRIME5;
//        }

//        h += (uint)length;

//        // process remaining 4-byte chunks
//        while (offset + 4 <= length)
//        {
//            uint k1 = (uint)(span[offset] | (span[offset + 1] << 8) | (span[offset + 2] << 16) | (span[offset + 3] << 24));
//            h = Rotl(h + k1 * PRIME3, 17) * PRIME4;
//            offset += 4;
//        }

//        // remaining bytes
//        while (offset < length)
//        {
//            h = Rotl(h + (uint)(span[offset]) * PRIME5, 11) * PRIME1;
//            offset++;
//        }

//        // avalanche
//        h ^= h >> 15;
//        h *= PRIME2;
//        h ^= h >> 13;
//        h *= PRIME3;
//        h ^= h >> 16;

//        return (int)h;
//    }
//}








//Section("Shared Memory Example");
//{
//    string s  = "Hello, World!";

//    Console.WriteLine($"Değer : {s}");
//    Console.WriteLine($"Uzunluk : {s.Length}");
//    Console.WriteLine($" [2] : {s[2]} - indexer , yeni allocation YOK");

//    // s[0]= "X";  // Hata verir, çünkü stringler immutable'dır.
//}

//Section("2 - fixed : GC'nin String'i taşımasını durdurma");
//{
//    string s = "Hello World";

//    unsafe
//    {
//        // Char pointer'ı kullanarak string'in bellekteki adresini alıyoruz.
//        fixed (char* p = s)
//        {
//            Console.WriteLine($"Pointer : {(int)p:X}");
//            Console.WriteLine($" s[2] : {p[2]} - indexer , yeni allocation YOK");
//            // 2 karakter sonrasına gitmek için pointer aritmetiği yapabiliriz.
//            Console.WriteLine($" *(ptr+2) : {*(p+2)} s[2] ");
//        }
//    }
//}

//Section("3- Pointer ile string karakter okuma (vs Indexer)");
//{
//    string s = "Hello World";
//    unsafe
//    {
//        fixed (char* ptr = s)
//        {
//            Console.Write(" Pointer ile : ");

//            char* p = ptr;

//            // Bound check yok, bu yüzden dikkatli olunmalı! Uygulama direkt çöker.
//            while (*p !='\0') // Null terminator'a kadar oku . .NET Stringleri null-terminated değildir,
//                              // ancak C#'ta fixed ile elde edilen char* pointer'lar null-terminated olarak davranır.
//            {
//                Console.Write(*p);
//                p++;
//            }
//             Console.WriteLine();
//             Console.Write(" Indexer ile : ");

//             for(int i=0; i<s.Length; i++)
//             {
//                Console.Write(s[i]); // Bound check var , bu yüzden güvenli ancak biraz daha yavaş olabilir.
//            }
//             Console.WriteLine();
//        }
//    }
//}

//Section("4 - Pointer Aritmetiği : sizeof ve Adres farklı");
//{
//    int[] numbers = { 10, 20, 30, 40, 50 };
//    unsafe
//    {
//        fixed (int* p = numbers)
//        {
//            Console.WriteLine($"Pointer : {(int)p:X}");
//            Console.WriteLine($" *p : { *p} - İlk eleman");
//            Console.WriteLine($" *(p+1) : { *(p+1)} - İkinci eleman");
//            Console.WriteLine($" sizeof(int) : {sizeof(int)} bytes");
//            Console.WriteLine($" Adres Farkı : {(int)(p + 1) - (int)p} bytes - sizeof(int) kadar");
//        }
//    }
//}

//Section("5 - String Mutation : Pointer ile değiştirme (Tehlikeli !)");
//{
//    string s = "Hello World";
//    Console.WriteLine($"Orijinal String : {s}");
//    unsafe
//    {
//        fixed (char* p = s)
//        {
//            // String'in ilk karakterini 'J' yapalım.
//            p[0] = 'J'; // Bu, string'in ilk karakterini değiştirecektir. Tehlikeli olabilir!
//            Console.WriteLine($"Değiştirilmiş String : {s}");
//        }
//    }
//}


//Section("6 - ReadonlySpan<char> vs Pointer:  Modern Alternatif");
//{
//    string s = "Hello World";
//    ReadOnlySpan<char> span = s.AsSpan();
//    Console.WriteLine("ReadOnlySpan<char> ile okuma:");
//    for (int i = 0; i < span.Length; i++)
//    {
//        Console.Write(span[i]);
//    }
//    Console.WriteLine();
//    // Pointer ile aynı işlemi yapalım. Span'i char pointer'a çevirelim.
//    unsafe
//    {
//        fixed (char* p = span)
//        {
//            Console.WriteLine("Pointer ile okuma:");
//            char* ptr = p;
//            while (*ptr != '\0')
//            {
//                Console.Write(*ptr);
//                ptr++;
//            }
//            Console.WriteLine();
//        }
//    }


//}

//// Pointerlar bellekteki veriyi kullanıcı tarafına getirmeden doğrudan erişim sağlar, bu da performans açısından avantajlı olabilir. Ancak, pointer kullanımı tehlikeli olabilir çünkü yanlış kullanıldığında bellek sızıntılarına, veri bozulmasına veya uygulama çökmelerine neden olabilir.
//// Bu nedenle, pointer kullanırken dikkatli olunmalı ve mümkünse modern alternatifler (örneğin, Span<T>) tercih edilmelidir.





//static void Section(string title)
//{
//    Console.WriteLine();
//    Console.ForegroundColor = ConsoleColor.DarkCyan;
//    Console.WriteLine(title);
//    Console.WriteLine(new string('-', 50));
//    Console.ResetColor();
//}