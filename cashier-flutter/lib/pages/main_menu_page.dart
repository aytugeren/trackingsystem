import 'package:flutter/material.dart';
import '../api/api_client.dart';
import 'home_page.dart';
import 'gold_price_screen.dart';
import 'profile_page.dart';
import 'leave_request_fixed.dart';

class MainMenuPage extends StatelessWidget {
  final ApiClient api;
  final Future<void> Function() onLogout;
  const MainMenuPage({super.key, required this.api, required this.onLogout});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Ana Ekran'),
        actions: [
          IconButton(
            onPressed: () async {
              await onLogout();
              if (context.mounted) {
                Navigator.of(context).popUntil((route) => route.isFirst);
              }
            },
            icon: const Icon(Icons.logout),
            tooltip: 'Çıkış',
          ),
        ],
      ),
      body: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          Text('Yeni İşlem', style: Theme.of(context).textTheme.titleLarge),
          const SizedBox(height: 8),
          Card(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  FilledButton.icon(
                    onPressed: () {
                      Navigator.of(context).push(MaterialPageRoute(
                        builder: (_) => HomePage(api: api, onLogout: onLogout, initialTab: 0),
                      ));
                    },
                    icon: const Icon(Icons.receipt_long),
                    label: const Text('Fatura Kes'),
                  ),
                  const SizedBox(height: 12),
                  FilledButton.tonalIcon(
                    onPressed: () {
                      Navigator.of(context).push(MaterialPageRoute(
                        builder: (_) => HomePage(api: api, onLogout: onLogout, initialTab: 1),
                      ));
                    },
                    icon: const Icon(Icons.money_off),
                    label: const Text('Gider Kes'),
                  ),
                ],
              ),
            ),
          ),
          const SizedBox(height: 16),
          Text('Diğer', style: Theme.of(context).textTheme.titleLarge),
          const SizedBox(height: 8),
          Card(
            child: ListTile(
              leading: const Icon(Icons.event_available),
              title: const Text('İzin İste'),
              subtitle: const Text('İzin talebi oluştur, takvimden ekibin izinlerini gör'),
              onTap: () {
                Navigator.of(context).push(MaterialPageRoute(
                  builder: (_) => LeaveRequestPage(api: api),
                ));
              },
            ),
          ),
          const SizedBox(height: 8),
          Card(
            child: ListTile(
              leading: const Icon(Icons.person),
              title: const Text('Profilim'),
              subtitle: const Text('Rol ve izin bilgilerini görüntüle'),
              onTap: () {
                Navigator.of(context).push(MaterialPageRoute(
                  builder: (_) => ProfilePage(api: api),
                ));
              },
            ),
          ),
          const SizedBox(height: 8),
          Card(
            child: ListTile(
              leading: const Icon(Icons.currency_exchange),
              title: const Text('Altın Fiyatları'),
              subtitle: const Text('Kapalıçarşı ve döviz fiyatlarını canlı takip edin'),
              onTap: () {
                Navigator.of(context).push(MaterialPageRoute(
                  builder: (_) => GoldPriceScreen(api: api),
                ));
              },
            ),
          ),
        ],
      ),
    );
  }
}

class _OldLeaveRequestPage extends StatelessWidget {
  final ApiClient api;
  const _OldLeaveRequestPage({super.key, required this.api});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('İzin İste')),
      body: const Center(
        child: Text('İzin talebi sayfası henüz eklenmedi'),
      ),
    );
  }
}
