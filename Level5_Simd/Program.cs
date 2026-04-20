
double x = 12.3;
double y = 4.2;

var sum = x + y;
var difference = x - y;

Console.WriteLine("Sonuç : 16.5 mi " + (sum == 16.5));

double a = 0.1;
double b = 0.2;

Console.WriteLine(a + b == 0.3);
// False çünkü : 0.1 ve 0.2'nin ikili temsilleri tam olarak 0.1 ve 0.2'ye eşit değildir, bu da toplama işleminin sonucunun tam olarak 0.3 olmamasına neden olur.

decimal aNew = 0.1m;
decimal bNew = 0.2m;

Console.WriteLine(aNew+ bNew == 0.3m); // True çünkü decimal türü, ondalık sayıları
                                       // daha yüksek bir doğrulukla temsil eder ve 0.1m ve 0.2m'nin temsilleri tam olarak 0.1m ve 0.2m'ye eşittir,
                                       // bu da toplama işleminin sonucunun tam olarak 0.3m olmasına neden olur.



static unsafe int ParseTemperatureBranchless(byte* ptr , int length)
{
    var sign = 1; // Pozitif mi

    if (ptr[0] == '-')
    {
        sign=-1;
        ptr++;
        length--;
    }

    int value;

    if (length == 3)
    {
        // 1.2 -> 12 
        value = ((ptr[0] - '0') * 10) + (ptr[2] - '0'); // 
    }
    else
    {
        // 12.3 -> 123
        value = ((ptr[0] - '0') * 100) +
                ((ptr[1] - '0') * 10)  +
                (ptr[3] - '0');
    }

    // Branchless
    return sign * value;
}