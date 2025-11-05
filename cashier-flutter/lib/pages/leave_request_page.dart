import 'package:flutter/material.dart';
import '../api/api_client.dart';
import '../api/leave_service.dart';
import 'package:intl/intl.dart';

class LeaveRequestPage extends StatefulWidget {
  final ApiClient api;
  const LeaveRequestPage({super.key, required this.api});

  @override
  State<LeaveRequestPage> createState() => _LeaveRequestPageState();
}

class _LeaveRequestPageState extends State<LeaveRequestPage> {
  DateTime _from = DateTime.now();
  DateTime _to = DateTime.now();
  bool _useTime = false;
  TimeOfDay _fromTime = const TimeOfDay(hour: 9, minute: 0);
  TimeOfDay _toTime = const TimeOfDay(hour: 13, minute: 0);
  final _reason = TextEditingController();
  bool _submitting = false;
  List<LeaveItem> _leaves = const [];
  bool _loading = true;
  DateTime _month = DateTime(DateTime.now().year, DateTime.now().month, 1);

  @override
  void initState() {
    super.initState();
    _loadLeaves();
  }

  Future<void> _loadLeaves() async {
    setState(() => _loading = true);
    try {
      final start = DateTime(_month.year, _month.month, 1);
      final end = DateTime(_month.year, _month.month + 1, 0);
      final items = await LeaveService(widget.api).listLeaves(from: start, to: end);
      if (!mounted) return;
      setState(() => _leaves = items);
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('İzin İste'),
        actions: [
          IconButton(onPressed: _loadLeaves, icon: const Icon(Icons.refresh)),
        ],
      ),
      body: SingleChildScrollView(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Text('Talep Oluştur', style: Theme.of(context).textTheme.titleLarge),
            // Saatli izin seçimi (yarım gün / saatlik)
            Row(children: [
              Checkbox(value: _useTime, onChanged: (v) {
                setState(() {
                  _useTime = v ?? false;
                  if (_useTime) _to = _from; // saatli izin tek gün
                });
              }),
              const Text('Saat seç (yarım gün / saatlik)')
            ]),
            if (_useTime) Row(children: [
              Expanded(child: _timeField('Başlangıç Saati', _fromTime, () async {
                final picked = await showTimePicker(context: context, initialTime: _fromTime);
                if (picked != null) { setState(() { _fromTime = picked; final sameDay = _from.year == _to.year && _from.month == _to.month && _from.day == _to.day; final fromMin = _fromTime.hour * 60 + _fromTime.minute; final toMin = _toTime.hour * 60 + _toTime.minute; if (sameDay && toMin <= fromMin) { _toTime = _fromTime; } }); }
              })),
              const SizedBox(width: 8),
              Expanded(child: _timeField('Bitiş Saati', _toTime, () async {
                final picked = await showTimePicker(context: context, initialTime: _toTime);
                if (picked != null) { final sameDay = _from.year == _to.year && _from.month == _to.month && _from.day == _to.day; final newMin = picked.hour * 60 + picked.minute; final fromMin = _fromTime.hour * 60 + _fromTime.minute; if (sameDay && newMin <= fromMin) { _show('Biti� saati ba�lang�� saatinden sonra olmal�'); } else { setState(() => _toTime = picked); } }
              })),
            ]),
            const SizedBox(height: 8),
            Row(
              children: [
                Expanded(child: _dateField('Başlangıç', _from, () async {
                  final picked = await showDatePicker(context: context, initialDate: _from, firstDate: DateTime(2020), lastDate: DateTime(2100));
                  if (picked != null) setState(() { _from = picked; _to = picked; });
                })),
                const SizedBox(width: 8),
                Expanded(child: _dateField('Bitiş', _to, () async {
                  final picked = await showDatePicker(context: context, initialDate: _to.isBefore(_from) ? _from : _to, firstDate: _from, lastDate: DateTime(2100));
                  if (picked != null) setState(() => _to = picked);
                })),
              ],
            ),
            const SizedBox(height: 8),
            TextField(controller: _reason, decoration: const InputDecoration(labelText: 'Açıklama')),
            const SizedBox(height: 12),
            FilledButton.icon(
              onPressed: _submitting ? null : _submit,
              icon: const Icon(Icons.send),
              label: Text(_submitting ? 'Gönderiliyor…' : 'Gönder'),
            ),
            const SizedBox(height: 24),
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Text('Takvim', style: Theme.of(context).textTheme.titleLarge),
                Row(children: [
                  IconButton(onPressed: () { setState(() { _month = DateTime(_month.year, _month.month - 1, 1); }); _loadLeaves(); }, icon: const Icon(Icons.chevron_left)),
                  Text(DateFormat('MMMM yyyy', 'tr_TR').format(_month)),
                  IconButton(onPressed: () { setState(() { _month = DateTime(_month.year, _month.month + 1, 1); }); _loadLeaves(); }, icon: const Icon(Icons.chevron_right)),
                ])
              ],
            ),
            const SizedBox(height: 8),
            if (_loading)
              const Center(child: CircularProgressIndicator())
            else
              _Calendar(
                month: _month,
                leaves: _leaves,
                onDayTap: (d) {
                  final matches = _leaves.where((l) => !d.isBefore(l.from) && !d.isAfter(l.to)).toList();
                  if (matches.isEmpty) return;
                  showModalBottomSheet(context: context, builder: (_) {
                    return Padding(
                      padding: const EdgeInsets.all(16),
                      child: Column(
                        mainAxisSize: MainAxisSize.min,
                        crossAxisAlignment: CrossAxisAlignment.stretch,
                        children: [
                          Text(DateFormat('d MMMM yyyy', 'tr_TR').format(d), style: Theme.of(context).textTheme.titleMedium),
                          const SizedBox(height: 8),
                          ...matches.map((m) => ListTile(
                                leading: Icon(Icons.circle, size: 12, color: _statusColor(m.status)),
                                title: Text(m.user),
                                subtitle: m.reason == null || m.reason!.isEmpty ? null : Text(m.reason!),
                              )),
                        ],
                      ),
                    );
                  });
                },
              ),
            const SizedBox(height: 16),
            Text('İzinler (Ay)', style: Theme.of(context).textTheme.titleLarge),
            const SizedBox(height: 8),
            _LeavesTable(leaves: _leaves),
          ],
        ),
      ),
    );
  }

  Widget _dateField(String label, DateTime d, Future<void> Function() onPick) {
    return OutlinedButton(
      onPressed: onPick,
      child: Row(
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: [
          Text(label),
          Text('${d.day.toString().padLeft(2, '0')}.${d.month.toString().padLeft(2, '0')}.${d.year}'),
        ],
      ),
    );
  }

  Widget _timeField(String label, TimeOfDay t, Future<void> Function() onPick) {
    String fmt(TimeOfDay tt) => '${tt.hour.toString().padLeft(2, '0')}:${tt.minute.toString().padLeft(2, '0')}';
    return OutlinedButton(
      onPressed: onPick,
      child: Row(
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: [
          Text(label),
          Text(fmt(t)),
        ],
      ),
    );
  }

  Future<void> _submit() async {
    if (_to.isBefore(_from)) { _show('Bitiş tarihi başlangıçtan önce olamaz'); return; }
    setState(() => _submitting = true);
    try {
      if (_useTime) {
        final fromMin = _fromTime.hour * 60 + _fromTime.minute;
        final toMin = _toTime.hour * 60 + _toTime.minute;
        if (toMin <= fromMin) { _show('Geçersiz saat aralığı'); return; }
      }
      String fmtTime(TimeOfDay t) => '${t.hour.toString().padLeft(2,'0')}:${t.minute.toString().padLeft(2,'0')}';
      await LeaveService(widget.api).createLeave(
        _from,
        _to,
        _reason.text.trim(),
        fromTime: _useTime ? fmtTime(_fromTime) : null,
        toTime: _useTime ? fmtTime(_toTime) : null,
      );
      if (!mounted) return;
      _reason.clear();
      _show('İzin talebi gönderildi');
      await _loadLeaves();
    } catch (e) {
      _show('İzin talebi gönderilemedi');
    } finally {
      if (mounted) setState(() => _submitting = false);
    }
  }

  void _show(String msg) {
    ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(msg)));
  }
}

// Ortak durum-renk eşleme
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

class _LeavesTable extends StatelessWidget {
  final List<LeaveItem> leaves;
  const _LeavesTable({required this.leaves});

  String _format(DateTime d) => '${d.day.toString().padLeft(2, '0')}.${d.month.toString().padLeft(2, '0')}.${d.year}';
  String _statusTr(String s) {
    switch (s.toLowerCase()) {
      case 'approved': return 'Onaylandı';
      case 'rejected': return 'Reddedildi';
      default: return 'Onay Bekliyor';
    }
  }

  @override
  Widget build(BuildContext context) {
    if (leaves.isEmpty) {
      return const Text('Bu ay için izin bulunmuyor');
    }
    return Card(
      child: SingleChildScrollView(
        scrollDirection: Axis.horizontal,
        child: ConstrainedBox(
          constraints: const BoxConstraints(minWidth: 560),
          child: Column(
            children: [
              Padding(
                padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
                child: Row(
                  children: const [
                    SizedBox(width: 160, child: Text('Tarih', style: TextStyle(fontWeight: FontWeight.bold))),
                    SizedBox(width: 160, child: Text('Kişi', style: TextStyle(fontWeight: FontWeight.bold))),
                    SizedBox(width: 80, child: Text('Durum', style: TextStyle(fontWeight: FontWeight.bold))),
                    SizedBox(width: 240, child: Text('Açıklama', style: TextStyle(fontWeight: FontWeight.bold))),
                  ],
                ),
              ),
              const Divider(height: 1),
              ...leaves.map((l) => Padding(
                    padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
                    child: Row(
                      children: [
                        SizedBox(width: 160, child: Text('${_format(l.from)} - ${_format(l.to)}')),
                        SizedBox(width: 160, child: Text(l.user, overflow: TextOverflow.ellipsis)),
                        SizedBox(
                          width: 80,
                          child: Row(children: [
                            Icon(Icons.circle, size: 12, color: _statusColor(l.status)),
                          ]),
                        ),
                        SizedBox(width: 240, child: Text(l.reason ?? '-', maxLines: 2, overflow: TextOverflow.ellipsis)),
                      ],
                    ),
                  )),
              const SizedBox(height: 8),
            ],
          ),
        ),
      ),
    );
  }
}

class _Calendar extends StatelessWidget {
  final DateTime month;
  final List<LeaveItem> leaves;
  final void Function(DateTime day)? onDayTap;
  const _Calendar({required this.month, required this.leaves, this.onDayTap});

  @override
  Widget build(BuildContext context) {
    // Basit ay görünümü: bugünkü ay
    final firstDay = DateTime(month.year, month.month, 1);
    final nextMonth = DateTime(month.year, month.month + 1, 1);
    final lastDay = nextMonth.subtract(const Duration(days: 1));
    final firstWeekday = firstDay.weekday % 7; // 0: Pazar
    final daysInMonth = lastDay.day;
    final totalCells = firstWeekday + daysInMonth;
    final rows = (totalCells / 7.0).ceil();

    Color? leaveColor(DateTime d) {
      // Öncelik: Approved (yeşil) > Pending (sarı) > Rejected (kırmızı)
      bool hasApproved = false, hasPending = false, hasRejected = false;
      for (final l in leaves) {
        if (d.isBefore(l.from) || d.isAfter(l.to)) continue;
        final s = l.status.toLowerCase();
        if (s == 'approved') hasApproved = true;
        else if (s == 'pending') hasPending = true;
        else if (s == 'rejected') hasRejected = true;
      }
      if (hasApproved) return Colors.green;
      if (hasPending) return Colors.amber;
      if (hasRejected) return Colors.red;
      return null;
    }

    return Column(
      children: List.generate(rows, (r) {
        return Row(
          children: List.generate(7, (c) {
            final idx = r * 7 + c;
            final dayNum = idx - firstWeekday + 1;
            final inMonth = dayNum >= 1 && dayNum <= daysInMonth;
            final date = inMonth ? DateTime(DateTime.now().year, DateTime.now().month, dayNum) : null;
            final color = date != null ? leaveColor(date) : null;
            final active = color != null;
            return Expanded(
              child: Container(
                margin: const EdgeInsets.all(2),
                padding: const EdgeInsets.symmetric(vertical: 10),
                decoration: BoxDecoration(
                  color: active ? color!.withOpacity(0.25) : Colors.transparent,
                  border: Border.all(color: Colors.grey.shade300),
                  borderRadius: BorderRadius.circular(8),
                ),
                alignment: Alignment.center,
                child: InkWell(
                  onTap: inMonth && active && onDayTap != null ? () => onDayTap!(date!) : null,
                  child: Padding(
                    padding: const EdgeInsets.symmetric(vertical: 4, horizontal: 2),
                    child: Text(inMonth ? '$dayNum' : ''),
                  ),
                ),
              ),
            );
          }),
        );
      }),
    );
  }
}