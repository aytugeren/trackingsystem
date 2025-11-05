Kasiyer Flutter Uygulaması
==========================

Bu klasör, Kuyumculuk Takip Programı için Kasiyer uygulamasının Flutter sürümünü içerir. Uygulama, .NET API ile JWT üzerinden haberleşir ve taslak fatura/gider oluşturma akışlarını sunar.

Özellikler
- Giriş (JWT): `/api/auth/login`
- Fatura taslağı oluşturma: `/api/cashier/invoices/draft`
- Gider taslağı oluşturma: `/api/cashier/expenses/draft`
- Güncel ALTIN fiyatını gösterme: `/api/pricing/current`

Gereksinimler
- Flutter 3.22+ (Dart 3)
- Backend API erişimi (varsayılan: `http://localhost:9002`)

Konfigürasyon
- API adresi, derleme zamanında `--dart-define=API_BASE=http://sunucu:port` ile geçilebilir.
  Örn: `flutter run --dart-define=API_BASE=http://localhost:9002`

Çalıştırma
1. Bağımlılıkları indir:
   - `flutter pub get`
2. Uygulamayı başlat:
   - `flutter run --dart-define=API_BASE=http://localhost:9002`

Notlar
- Taslak uç noktalar, SıraNo değerini sunucuda atar. Formda girilen `SıraNo` yok sayılır.
- Tarih alanı `DateOnly (yyyy-MM-dd)` olarak gönderilir.
- `OdemeSekli`: Havale (0), KrediKartı (1)
- `AltinAyar`: 22 veya 24

Klasör Yapısı
- `lib/api`: HTTP istemcisi, servisler ve modeller
- `lib/pages`: Login ve ana (kasiyer) ekranı

