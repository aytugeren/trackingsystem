import 'package:flutter/material.dart';
import '../api/api_client.dart';
import '../api/profile_service.dart';
import '../api/leave_service.dart';

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
  List<LeaveItem> _myLeaves = const [];

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    if (!mounted) return;
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final svc = ProfileService(widget.api);
      final me = await svc.getMe();
      List<LeaveItem> leaves = const [];
      if (me?.email != null && me!.email!.isNotEmpty) {
        final now = DateTime.now();
        final start = DateTime(now.year, 1, 1);
        final end = DateTime(now.year, 12, 31);
        final all = await LeaveService(widget.api).listLeaves(from: start, to: end);
        leaves = all.where((l) => l.user == me.email).toList();
      }
      if (!mounted) return;
      setState(() {
        _me = me;
        _myLeaves = leaves;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _error = 'Profil alınamadı';
        _me = null;
        _myLeaves = const [];
      });
    } finally {
      if (!mounted) return;
      setState(() {
        _loading = false;
      });
    }
  }

  String _fmt(DateTime d) => '${d.day.toString().padLeft(2, '0')}.${d.month.toString().padLeft(2, '0')}.${d.year}';
  String _statusTr(String s) {
    switch (s.toLowerCase()) {
      case 'approved':
        return 'Onaylandı';
      case 'rejected':
        return 'Reddedildi';
      default:
        return 'Bekliyor';
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
                      child: SingleChildScrollView(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            _InfoRow(label: 'Email', value: _me!.email ?? '-'),
                            const SizedBox(height: 8),
                            _InfoRow(label: 'Rol', value: _me!.role ?? '-'),
                            const SizedBox(height: 8),
                            _InfoRow(label: 'Yıllık izin hakkı (gün)', value: _me!.allowanceDays.toString()),
                            const SizedBox(height: 8),
                            _InfoRow(label: 'Kullanılan (gün)', value: _me!.usedDays.toStringAsFixed(2)),
                            const SizedBox(height: 8),
                            _InfoRow(label: 'Kalan (gün)', value: _me!.remainingDays.toStringAsFixed(2)),
                            const SizedBox(height: 16),
                            Text('İzinlerim', style: Theme.of(context).textTheme.titleMedium),
                            const SizedBox(height: 8),
                            _myLeaves.isEmpty
                                ? const Text('İzin yok')
                                : Card(
                                    child: ListView.separated(
                                      shrinkWrap: true,
                                      physics: const NeverScrollableScrollPhysics(),
                                      itemCount: _myLeaves.length,
                                      separatorBuilder: (_, __) => const Divider(height: 1),
                                      itemBuilder: (_, i) {
                                        final l = _myLeaves[i];
                                        final same = l.from.year == l.to.year && l.from.month == l.to.month && l.from.day == l.to.day;
                                        final range = same ? _fmt(l.from) : '${_fmt(l.from)} - ${_fmt(l.to)}';
                                        final times = (l.fromTime != null && l.toTime != null) ? ' (${l.fromTime}-${l.toTime})' : '';
                                        return ListTile(
                                          leading: Icon(Icons.circle, size: 12, color: _statusColor(l.status)),
                                          title: Text(range + (same ? times : '')),
                                          subtitle: Text(l.reason ?? '-'),
                                          trailing: Text(_statusTr(l.status)),
                                        );
                                      },
                                    ),
                                  ),
                            const SizedBox(height: 24),
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

// Map leave status to a display color
Color _statusColor(String status) {
  switch (status.toLowerCase()) {
    case 'approved':
      return Colors.green;
    case 'rejected':
      return Colors.red;
    default:
      return Colors.amber;
  }
}

