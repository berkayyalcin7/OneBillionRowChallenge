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

