import 'package:flutter/material.dart';
import '../api/api_client.dart';

class GoldPriceScreen extends StatelessWidget {
  final ApiClient api;
  const GoldPriceScreen({super.key, required this.api});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Altin fiyatlari')),
      body: const Center(
        child: Padding(
          padding: EdgeInsets.all(16),
          child: Text(
            'Altin fiyat ekrani devre disi birakildi. Yeni saglayici hazir olunca yeniden acilacak.',
            textAlign: TextAlign.center,
          ),
        ),
      ),
    );
  }
}
