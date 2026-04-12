# CPU (Central Processing Unit) - İşlemci Çekirdeği

## Giriş
İşlemci Çekirdeği (CPU), bilgisayarın merkezi işlem birimidir ve temel olarak bilgisayarın tüm işlemlerini yönetir. CPU, komutları işler, verileri işler ve diğer bileşenlerle iletişim kurar. 

### CPU'nun Temel Özellikleri
- **Çok Çekirdekli Yapı**: Modern işlemciler birden fazla çekirdek içerebilir, bu da aynı anda birden fazla işlemi gerçekleştirebilme yeteneği sağlar
- **Performans**: Modern işlemciler genellikle çok çekirdekli yapıya sahiptir, bu da performansı artırır ve çoklu görevleri daha verimli hale getirir
- **Hız**: CPU'nun hızı, genellikle gigahertz (GHz) cinsinden ölçülür ve daha yüksek hızlar genellikle daha iyi performans anlamına gelir

---

## Tek Çekirdek vs. Çok Çekirdek

### Tek Çekirdek
- Kendisine verilen komutları sırayla işler
- Bir anda sadece bir işlem gerçekleştirebilir

### Çok Çekirdekli İşlemciler
- Aynı anda birden fazla komutu işleyebilir
- Çoklu görevlerde ve paralel işlemlerde daha iyi performans gösterir

---

## Thread Nedir?

**Threadler** ise sanal bir işlemci çekirdeği gibi davranan, işletim sistemi tarafından yönetilen ve CPU tarafından işlenen küçük birimlerdir.

### Tek Çekirdekte Çalışan Uygulamalar
Biz uygulamamızı tek bir çekirdekte çalıştırıyorsak ve uygulamamızın çok fazla işlem yapması gerekiyorsa, bu durum:
- CPU'nun tek bir çekirdeğinin aşırı yüklenmesine
- Performansın düşmesine neden olabilir

---

## Paralel İşleme: Dosya Okuma Örneği

### Çok Çekirdekli Dosya İşleme
Bir örneğin bir txt dosyasını okumak istiyorsak, aynı anda bunu core'lara ayırarak yapabiliriz:
- Dosyayı parçalara bölüyoruz
- Her parçayı farklı bir çekirdekte işliyoruz
- Sonuçları birleştiriyoruz

---

## Race Condition (Yarış Durumu)

### Problem
Paralel işleme yaparken **Race Condition** gibi durumlar ortaya çıkabilir:
- Aynı key değerine sahip iki thread aynı anda dosyaya yazmaya çalışırsa
- Bu durum **veri tutarsızlığına** ve **hatalara** yol açabilir

### Çözüm: Lock Mekanizması
Dosyaya yazarken bir kilit (lock) mekanizması kullanarak:
- Aynı anda sadece bir çekirdeğin dosyaya yazmasını sağlayabiliriz
- Diğer çekirdekler işlem bitene kadar bekler

### ⚠️ Lock'ın Sorunları
- Performans kaybına neden olur
- Diğer çekirdekler kilit açılana kadar beklemek zorunda kalır
- Bu tür durumlarda **Amdalh's Law** ortaya çıkar

---

## Amdalh's Law (Amdal'ın Kanunu)

### Tanım
Amdalh's Law, bir programın paralel olarak çalıştırılabilen kısmının performansını sınırlayan bir teoridir.

### Örnek Hesaplama
Bir programın:
- **%90'ı** paralel olarak çalıştırılabiliyorsa
- **%10'u** seri olarak çalıştırılması gerekiyorsa
- **Maksimum hızlanma = 10 kat** olacaktır

> Formül: `Max Speedup = 1 / (Serial Portion + Parallel Portion / N)`
> 
> Burada N = çekirdek sayısı

### Önem
Paralel programlama yaparken:
- ✅ Programın **paralel olarak çalıştırılabilen kısmını** artırmak
- ✅ **Seri kısmını** azaltmak önemlidir

---

## Gerçek Hayat Örneği: Web Sunucusu

### Senaryo
Bir web sunucusu, gelen istekleri işlemek için birden fazla çekirdek kullanabilir.

### Performans Sorunu
Bazı işlemler seri olarak çalıştırılması gerekebilir:
- Veritabanı sorguları
- Kimlik doğrulama işlemleri
- Yazma işlemleri

### Sonuç
Bu durumda, web sunucusunun performansı **Amdalh's Law** tarafından sınırlanabilir.

### Optimizasyon Stratejileri
1. **Veritabanı sorgularını** paralel olarak çalıştırmak
2. **Seri işlemleri** optimize etmek
3. **Cache mekanizmaları** kullanmak
4. **Asenkron işlemler** tercih etmek

---

## Özet

| Kavram | Açıklama |
|--------|----------|
| **CPU** | Merkezi işlem birimi, bilgisayarın tüm işlemlerini yönetir |
| **Çekirdek** | Bağımsız bir işlem birimi, paralel işleme sağlar |
| **Thread** | İşletim sistemi tarafından yönetilen sanal işlem birimi |
| **Paralel İşleme** | Çoklu görevleri aynı anda gerçekleştirme |
| **Race Condition** | Aynı kaynağa birden fazla thread erişmesi |
| **Lock** | Kaynak erişimini kontrol etme mekanizması |
| **Amdalh's Law** | Paralel programlamada maksimum hızlanmayı sınırlayan teori |
