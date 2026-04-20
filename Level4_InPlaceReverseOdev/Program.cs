
char[] charArray = "Merhaba".ToCharArray();

unsafe
{
    // Dizinin bellekteki adresini sabitliyoruz
    fixed (char* pBase = charArray)
    {
        char* left = pBase;                      // İlk karakterin adresi
        char* right = pBase + charArray.Length - 1; // Son karakterin adresi

        while (left < right)
        {
            // Bellek adresindeki değerleri takas et (Swap)
            char temp = *left;
            *left = *right;
            *right = temp;

            // Pointerları merkeze doğru kaydır
            left++;
            right--;
        }
    }
}

Console.WriteLine(new string(charArray)); // Çıktı: abahreM
 