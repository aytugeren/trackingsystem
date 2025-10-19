Kuyumculuk Takip Programı — Portainer Deploy

Özet

- Tek docker-compose ile API (.NET), iki Next.js frontend (dashboard + cashier) ve Postgres ayağa kalkar.
- Varsayılan network: `kuyumculuk-net` (compose içinde tanımlı). İsterseniz Nginx Manager ile aynı ağa da ekleyebilirsiniz.
- Varsayılan portlar: API `8080`, Cashier `3000`, Dashboard `3001`, PgAdmin `5050`.

Önkoşullar

- VPS Linux sunucu (Docker + Portainer kurulu).
- Gerekirse alan adları Nginx Manager üzerinden yönlendirilir (proje içinde ayrıca ayar gerekmez).

Ortam Değişkenleri

- Tüm servisler docker-compose içinde environment değişkenleri ile yönetilir.
- `ops/.env.example` dosyasını kopyalayıp `ops/.env` olarak düzenleyebilirsiniz; Portainer Stack ekranında da bu değişkenleri tek tek tanımlayabilirsiniz.
- Önemli: `JWT_KEY` üretimde mutlaka güçlü ve uzun bir değer olmalı.

Yayınlama (Portainer Stack)

1) Portainer > Stacks > Add Stack
2) Repository’den deploy ediyorsanız repo yolunu ve `ops/docker-compose.yml` yolunu gösterin.
   - Alternatif: `ops/docker-compose.yml` içeriğini direkt yapıştırın.
3) Environment Variables
   - Stack oluştururken değişken alanlarına `ops/.env.example` içindeki anahtarları girin.
   - Ya da `ops/.env` dosyasını kullanmak için Portainer’da “.env file” seçeneğini işaretleyin (varsa).
   - `JWT_KEY` değerini mutlaka değiştirin.
4) Deploy
   - Stack deploy edildiğinde sırasıyla Postgres, API ve frontendl’er ayağa kalkar.
   - Sağlık kontrolleri: API `/health`, frontend kök `/`.

Ağ ve Reverse Proxy (Opsiyonel)

- Nginx Manager kullanıyorsanız iki yöntem:
  1) Host portları (8080/3000/3001) üzerinden yönlendirin.
  2) Nginx Manager’ın kullandığı external network’e bu servisleri ekleyin ve container ismi:port’a proxy verin.
- Compose içinde default network `kuyumculuk-net`. Harici bir network’e eklemek için örnek:
  ```yaml
  networks:
    default:
      name: kuyumculuk-net
    proxy:
      external: true

  services:
    api:
      networks:
        - default
        - proxy
    dashboard:
      networks:
        - default
        - proxy
    cashier:
      networks:
        - default
        - proxy
  ```

Komutlar (Portainer dışı, direkt Docker Compose ile)

- `cd ops && cp .env.example .env && docker compose up -d --build`
- Loglar: `docker compose logs -f api` vb.

Varsayılan Erişimler

- API: `http://<sunucu-ip>:8080`
- Dashboard: `http://<sunucu-ip>:3001`
- Cashier: `http://<sunucu-ip>:3000`
- PgAdmin: `http://<sunucu-ip>:5050` (PGADMIN_DEFAULT_EMAIL/PASSWORD env’lerine bakınız)

Notlar

- İlk açılışta API migration ve seed işlemlerini çalıştırır; Postgres’e bağlanabildiğini loglarda görürsünüz.
- Güvenlik için `Jwt__Key`’i zorunlu değiştirin ve PgAdmin’i dış dünyaya kapatmayı değerlendirin.
