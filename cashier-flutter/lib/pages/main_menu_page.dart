import 'package:flutter/material.dart';

import '../api/api_client.dart';
import 'gold_prices_page.dart';
import 'profile_page.dart';
import 'leave_request_fixed.dart';
import 'home_page.dart';

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
              if (!context.mounted) return;
              Navigator.of(context).pop();
            },
            icon: const Icon(Icons.logout),
            tooltip: 'Cikis',
          ),
        ],
      ),
      body: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          Text('Yeni Islem', style: Theme.of(context).textTheme.titleLarge),
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
                        builder: (_) => HomePage(
                            api: api, onLogout: onLogout, initialTab: 0),
                      ));
                    },
                    icon: const Icon(Icons.receipt_long),
                    label: const Text('Fatura Kes'),
                  ),
                  const SizedBox(height: 12),
                  FilledButton.tonalIcon(
                    onPressed: () {
                      Navigator.of(context).push(MaterialPageRoute(
                        builder: (_) => HomePage(
                            api: api, onLogout: onLogout, initialTab: 1),
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
          Text('Diger', style: Theme.of(context).textTheme.titleLarge),
          const SizedBox(height: 8),
          Card(
            child: ListTile(
              leading: const Icon(Icons.event_available),
              title: const Text('Izin Ist'),
              subtitle: const Text(
                  'Izin talebi olustur, takvimden ekibin izinlerini gor'),
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
              leading: const Icon(Icons.currency_lira),
              title: const Text('Altin Fiyatlari'),
              subtitle: const Text('Has altin ve piyasa fiyatlarini gor'),
              onTap: () {
                Navigator.of(context).push(MaterialPageRoute(
                  builder: (_) => GoldPricesPage(api: api),
                ));
              },
            ),
          ),
          const SizedBox(height: 8),
          Card(
            child: ListTile(
              leading: const Icon(Icons.person),
              title: const Text('Profilim'),
              subtitle: const Text('Rol ve izin bilgilerini goruntule'),
              onTap: () {
                Navigator.of(context).push(MaterialPageRoute(
                  builder: (_) => ProfilePage(api: api),
                ));
              },
            ),
          ),
        ],
      ),
    );
  }
}
