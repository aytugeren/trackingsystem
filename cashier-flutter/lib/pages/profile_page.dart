import 'package:flutter/material.dart';
import '../api/api_client.dart';
import '../api/profile_service.dart';

class ProfilePage extends StatefulWidget {
  final ApiClient api;
  const ProfilePage({super.key, required this.api});

  @override
  State<ProfilePage> createState() => _ProfilePageState();
}

class _ProfilePageState extends State<ProfilePage> {
  ProfileMe? _me;
  bool _loading = true;
  String? _error;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() { _loading = true; _error = null; });
    try {
      final svc = ProfileService(widget.api);
      final me = await svc.getMe();
      setState(() { _me = me; });
    } catch (e) {
      setState(() { _error = 'Profil alınamadı'; });
    } finally {
      setState(() { _loading = false; });
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Profilim')),
      body: _loading
          ? const Center(child: CircularProgressIndicator())
          : _error != null
              ? Center(child: Text(_error!, style: const TextStyle(color: Colors.red)))
              : _me == null
                  ? const Center(child: Text('Bilgi bulunamadı'))
                  : Padding(
                      padding: const EdgeInsets.all(16),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          _InfoRow(label: 'Email', value: _me!.email ?? '-'),
                          const SizedBox(height: 8),
                          _InfoRow(label: 'Rol', value: _me!.role ?? '-'),
                          const SizedBox(height: 8),
                          _InfoRow(label: 'Yıllık İzin Hakkı (gün)', value: _me!.allowanceDays.toString()),
                          const SizedBox(height: 8),
                          _InfoRow(label: 'Kullanılan (gün)', value: _me!.usedDays.toStringAsFixed(2)),
                          const SizedBox(height: 8),
                          _InfoRow(label: 'Kalan (gün)', value: _me!.remainingDays.toStringAsFixed(2)),
                          const Spacer(),
                          Row(
                            children: [
                              ElevatedButton.icon(
                                onPressed: _load,
                                icon: const Icon(Icons.refresh),
                                label: const Text('Yenile'),
                              ),
                            ],
                          )
                        ],
                      ),
                    ),
    );
  }
}

class _InfoRow extends StatelessWidget {
  final String label;
  final String value;
  const _InfoRow({required this.label, required this.value});

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        SizedBox(width: 200, child: Text(label, style: const TextStyle(fontWeight: FontWeight.w600))),
        Expanded(child: Text(value)),
      ],
    );
  }
}

