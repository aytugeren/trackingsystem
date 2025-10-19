Kuyumculuk Takip Programı — Portainer Deploy

Özet

- Tek docker-compose ile API (.NET), iki Next.js frontend (dashboard + cashier) ve Postgres ayağa kalkar.
- Varsayılan network: `kuyumculuk-net` (compose içinde tanımlı). İsterseniz Nginx Manager ile aynı ağa da ekleyebilirsiniz.
- Varsayılan portlar: API `8080`, Cashier `3000`, Dashboard `3001`, PgAdmin `5050`.

Önkoşullar

- VPS Linux sunucu (Docker + Portainer kurulu).
- Gerekirse alan adları Nginx Manager üzerinden yönlendirilir (proje içinde ayrıca ayar gerekmez).

Ortam Değişkenleri

- Backend: `ops/env.backend`
  - `ASPNETCORE_ENVIRONMENT=Production` olarak ayarlı.
  - `Jwt__Key` üretim için güçlü ve uzun bir secret ile doldurun.
  - Postgres bağlantıları compose içindeki `postgres` servisine ayarlı.
- Frontend Dashboard: `ops/env.frontend-dashboard`
  - `NODE_ENV=production`
  - `NEXT_PUBLIC_API_BASE` boş (aynı origin). Next.js rewrites `/api` isteklerini API konteynerine yönlendirir.
- Frontend Cashier: `ops/env.frontend-cashier`
  - `NODE_ENV=production`
  - `NEXT_PUBLIC_API_URL` boş (aynı origin). Gerekirse public API URL’i verilebilir.
  - `API_REWRITE_TARGET` build sırasında API servisine (`http://api:8080`) yönlendirir.

Yayınlama (Portainer Stack)

1) Portainer > Stacks > Add Stack
2) Repository’den deploy ediyorsanız repo yolunu ve `ops/docker-compose.yml` yolunu gösterin.
   - Alternatif: `ops/docker-compose.yml` içeriğini direkt yapıştırın.
3) Env/Secrets
   - `ops/env.backend`, `ops/env.frontend-dashboard`, `ops/env.frontend-cashier` dosyalarını Stack altında Environment vars olarak ekleyebilirsiniz veya compose’daki `env_file` kullanımını olduğu gibi bırakabilirsiniz (Git’ten geliyorsa). `Jwt__Key` değerini mutlaka değiştirin.
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

- `cd ops && docker compose up -d --build`
- Loglar: `docker compose logs -f api` vb.

Varsayılan Erişimler

- API: `http://<sunucu-ip>:8080`
- Dashboard: `http://<sunucu-ip>:3001`
- Cashier: `http://<sunucu-ip>:3000`
- PgAdmin: `http://<sunucu-ip>:5050` (PGADMIN_DEFAULT_EMAIL/PASSWORD env’lerine bakınız)

Notlar

- İlk açılışta API migration ve seed işlemlerini çalıştırır; Postgres’e bağlanabildiğini loglarda görürsünüz.
- Güvenlik için `Jwt__Key`’i zorunlu değiştirin ve PgAdmin’i dış dünyaya kapatmayı değerlendirin.
