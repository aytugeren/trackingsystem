import 'dart:async';

import 'package:flutter/material.dart';
import 'package:intl/intl.dart';

import '../api/api_client.dart';
import '../api/models.dart';
import '../api/pricing_service.dart';

enum _PriceMode { alis, satis }

class GoldPricesPage extends StatefulWidget {
  final ApiClient api;
  const GoldPricesPage({super.key, required this.api});

  @override
  State<GoldPricesPage> createState() => _GoldPricesPageState();
}

class _GoldPricesPageState extends State<GoldPricesPage> {
  GoldPrice? _goldInfo;
  GoldFeedLatest? _feed;
  bool _goldLoading = true;
  bool _feedLoading = true;
  bool _editingGold = false;
  bool _savingGold = false;
  String? _goldError;
  String? _feedError;
  final TextEditingController _goldController = TextEditingController();
  final TextEditingController _searchController = TextEditingController();
  _PriceMode _mode = _PriceMode.satis;
  Timer? _searchTimer;
  String _search = '';

  @override
  void initState() {
    super.initState();
    _loadGold();
    _loadFeed();
  }

  @override
  void dispose() {
    _goldController.dispose();
    _searchController.dispose();
    _searchTimer?.cancel();
    super.dispose();
  }

  Future<void> _loadGold() async {
    setState(() {
      _goldLoading = true;
      _goldError = null;
    });
    try {
      final info = await PricingService(widget.api).gold();
      if (!mounted) return;
      setState(() {
        _goldInfo = info;
        _goldController.text =
            info == null ? '' : info.price.toStringAsFixed(3);
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _goldError = 'Has altın bilgisi alınamadı';
      });
    } finally {
      if (!mounted) return;
      setState(() => _goldLoading = false);
    }
  }

  Future<void> _saveGold() async {
    final raw = _goldController.text.trim().replaceAll(',', '.');
    final parsed = double.tryParse(raw);
    if (parsed == null || parsed <= 0) {
      _showSnack('Geçerli bir has altın değeri girin');
      return;
    }
    setState(() {
      _savingGold = true;
      _goldError = null;
    });
    try {
      final updated = await PricingService(widget.api).updateGold(parsed);
      if (!mounted) return;
      setState(() {
        _goldInfo = updated;
        _editingGold = false;
        _goldController.text = updated.price.toStringAsFixed(3);
      });
    } catch (e) {
      if (!mounted) return;
      setState(() => _goldError = _formatApiError(e, 'Has altın güncellenemedi'));
    } finally {
      if (!mounted) return;
      setState(() => _savingGold = false);
    }
  }

  Future<void> _loadFeed() async {
    setState(() {
      _feedLoading = true;
      _feedError = null;
    });
    final res = await PricingService(widget.api).feedLatest();
    if (!mounted) return;
    setState(() {
      _feed = res;
      _feedLoading = false;
      _feedError = res == null ? 'Altın fiyatları alınamadı' : null;
    });
  }

  void _onSearchChanged(String value) {
    _searchTimer?.cancel();
    _searchTimer = Timer(const Duration(milliseconds: 200), () {
      if (!mounted) return;
      setState(() => _search = value.trim());
    });
  }

  String _formatApiError(Object err, String fallback) {
    if (err is ApiError) {
      final msg = err.body['error'] ?? err.body['message'];
      if (msg is String && msg.trim().isNotEmpty) return msg;
      return '$fallback (HTTP ${err.status})';
    }
    return fallback;
  }

  List<GoldFeedItem> _filteredItems() {
    final items = _feed?.items.where((it) => it.isUsed).toList() ?? [];
    if (items.isEmpty) return [];
    final query = _normalize(_search);
    return items.where((item) {
      final label = _normalize(item.label);
      final isBuy = label.contains('alis');
      final isSell = label.contains('satis');
      if (_mode == _PriceMode.alis && !isBuy) return false;
      if (_mode == _PriceMode.satis && !isSell) return false;
      if (query.isEmpty) return true;
      return label.contains(query) || item.index.toString().contains(query);
    }).toList();
  }

  String _normalize(String value) {
    return value
        .toLowerCase()
        .replaceAll('ı', 'i')
        .replaceAll('İ', 'i')
        .replaceAll('ş', 's')
        .replaceAll('Ş', 's')
        .replaceAll('ğ', 'g')
        .replaceAll('Ğ', 'g')
        .replaceAll('ü', 'u')
        .replaceAll('Ü', 'u')
        .replaceAll('ö', 'o')
        .replaceAll('Ö', 'o')
        .replaceAll('ç', 'c')
        .replaceAll('Ç', 'c');
  }

  String _formatNumber(num? value, {int fractionDigits = 3}) {
    if (value == null) return '-';
    final f = NumberFormat.decimalPattern('tr_TR');
    f.maximumFractionDigits = fractionDigits;
    f.minimumFractionDigits = 0;
    return f.format(value);
  }

  String _formatCurrency(num? value) {
    if (value == null) return 'Has altın girilmedi';
    final f =
        NumberFormat.currency(locale: 'tr_TR', symbol: '₺', decimalDigits: 3);
    return f.format(value);
  }

  String _formatMeta(GoldPrice? info) {
    if (info == null) return 'Henüz güncellenmedi';
    final updatedAt = info.updatedAt;
    if (updatedAt == null && (info.updatedBy ?? '').isEmpty) {
      return 'Henüz güncellenmedi';
    }
    final parts = <String>[];
    if (updatedAt != null) {
      parts.add(DateFormat('dd.MM.yyyy HH:mm', 'tr_TR').format(updatedAt));
    }
    if ((info.updatedBy ?? '').isNotEmpty) {
      parts.add(info.updatedBy!);
    }
    return parts.isEmpty ? 'Henüz güncellenmedi' : 'Son: ${parts.join(' - ')}';
  }

  void _showSnack(String msg) {
    ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(msg)));
  }

  @override
  Widget build(BuildContext context) {
    final filtered = _filteredItems();
    final header = _feed?.header;
    return Scaffold(
      appBar: AppBar(
        title: const Text('Altın Fiyatları'),
      ),
      body: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          if (_goldError != null)
            Card(
              color: const Color(0xFFFDECEA),
              child: Padding(
                padding: const EdgeInsets.all(12),
                child: Text(_goldError!,
                    style: const TextStyle(
                        color: Color(0xFFB3261E), fontWeight: FontWeight.w600)),
              ),
            ),
          Card(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: _goldLoading
                  ? const Row(
                      children: [
                        SizedBox(
                            width: 18,
                            height: 18,
                            child: CircularProgressIndicator(strokeWidth: 2)),
                        SizedBox(width: 8),
                        Text('Yükleniyor...'),
                      ],
                    )
                  : _editingGold
                      ? Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            const Text('Has Altın',
                                style: TextStyle(fontWeight: FontWeight.w700)),
                            const SizedBox(height: 8),
                            TextField(
                              controller: _goldController,
                              keyboardType:
                                  const TextInputType.numberWithOptions(
                                      decimal: true),
                              decoration: const InputDecoration(
                                hintText: 'Has altın fiyatı',
                                border: OutlineInputBorder(),
                              ),
                            ),
                            const SizedBox(height: 12),
                            Row(
                              children: [
                                FilledButton(
                                  onPressed: _savingGold ? null : _saveGold,
                                  child: Text(
                                      _savingGold ? 'Kaydediliyor…' : 'Kaydet'),
                                ),
                                const SizedBox(width: 8),
                                TextButton(
                                  onPressed: _savingGold
                                      ? null
                                      : () => setState(() =>
                                          _editingGold = false),
                                  child: const Text('Vazgeç'),
                                ),
                              ],
                            ),
                          ],
                        )
                      : InkWell(
                          onTap: () {
                            setState(() => _editingGold = true);
                          },
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              const Text('Has Altın',
                                  style:
                                      TextStyle(fontWeight: FontWeight.w700)),
                              const SizedBox(height: 6),
                              Text(
                                _formatCurrency(_goldInfo?.price),
                                style: const TextStyle(
                                    fontSize: 22, fontWeight: FontWeight.w800),
                              ),
                              const SizedBox(height: 6),
                              Text(
                                _formatMeta(_goldInfo),
                                style: const TextStyle(
                                    fontSize: 11, color: Color(0xFF666666)),
                              ),
                            ],
                          ),
                        ),
            ),
          ),
          const SizedBox(height: 16),
          Card(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    children: [
                      const Expanded(
                        child: Text('Altın Fiyatları',
                            style: TextStyle(fontWeight: FontWeight.w700)),
                      ),
                      TextButton.icon(
                        onPressed: _feedLoading ? null : _loadFeed,
                        icon: const Icon(Icons.refresh, size: 18),
                        label: Text(_feedLoading ? 'Yükleniyor…' : 'Yenile'),
                      ),
                    ],
                  ),
                  const SizedBox(height: 8),
                  Wrap(
                    spacing: 8,
                    runSpacing: 8,
                    children: [
                      ToggleButtons(
                        isSelected: [
                          _mode == _PriceMode.alis,
                          _mode == _PriceMode.satis
                        ],
                        onPressed: (i) => setState(() => _mode =
                            i == 0 ? _PriceMode.alis : _PriceMode.satis),
                        children: const [
                          Padding(
                            padding: EdgeInsets.symmetric(horizontal: 12),
                            child: Text('Alış'),
                          ),
                          Padding(
                            padding: EdgeInsets.symmetric(horizontal: 12),
                            child: Text('Satış'),
                          ),
                        ],
                      ),
                      SizedBox(
                        width: 220,
                        child: TextField(
                          controller: _searchController,
                          onChanged: _onSearchChanged,
                          decoration: const InputDecoration(
                            hintText: 'Ürün adı ile ara',
                            border: OutlineInputBorder(),
                            isDense: true,
                          ),
                        ),
                      ),
                      TextButton(
                        onPressed: (_searchController.text.isEmpty &&
                                _search.isEmpty)
                            ? null
                            : () {
                                _searchController.clear();
                                setState(() => _search = '');
                              },
                        child: const Text('Temizle'),
                      ),
                    ],
                  ),
                  const SizedBox(height: 12),
                  if (_feedError != null)
                    Container(
                      padding: const EdgeInsets.all(10),
                      decoration: BoxDecoration(
                        color: const Color(0xFFFDECEA),
                        borderRadius: BorderRadius.circular(6),
                      ),
                      child: Text(_feedError!,
                          style: const TextStyle(
                              color: Color(0xFFB3261E),
                              fontWeight: FontWeight.w600)),
                    ),
                  if (_feedLoading)
                    const Padding(
                      padding: EdgeInsets.symmetric(vertical: 12),
                      child: Row(
                        children: [
                          SizedBox(
                              width: 18,
                              height: 18,
                              child:
                                  CircularProgressIndicator(strokeWidth: 2)),
                          SizedBox(width: 8),
                          Text('Yükleniyor...'),
                        ],
                      ),
                    ),
                  if (!_feedLoading && header != null) ...[
                    Text(
                      'Son çekim: ${DateFormat('dd.MM.yyyy HH:mm', 'tr_TR').format(_feed!.fetchedAt)}',
                      style:
                          const TextStyle(fontSize: 11, color: Colors.black54),
                    ),
                    const SizedBox(height: 8),
                    Table(
                      columnWidths: const {
                        0: FlexColumnWidth(1),
                        1: FlexColumnWidth(1),
                      },
                      children: [
                        _row('USD Alış', _formatNumber(header.usdAlis)),
                        _row('USD Satış', _formatNumber(header.usdSatis)),
                        _row('EURO Alış', _formatNumber(header.eurAlis)),
                        _row('EURO Satış', _formatNumber(header.eurSatis)),
                        _row('EURO/USD', _formatNumber(header.eurUsd)),
                        _row('ONS', _formatNumber(header.ons)),
                        _row('HAS', _formatNumber(header.has)),
                        _row('GümüşHAS', _formatNumber(header.gumusHas)),
                      ],
                    ),
                    const SizedBox(height: 12),
                    const Text('Sabit Liste',
                        style: TextStyle(fontWeight: FontWeight.w700)),
                    const SizedBox(height: 8),
                    if (filtered.isEmpty)
                      const Text('Sonuç bulunamadı.',
                          style: TextStyle(color: Colors.black54))
                    else
                      Column(
                        children: filtered
                            .map((item) => Padding(
                                  padding:
                                      const EdgeInsets.symmetric(vertical: 4),
                                  child: Row(
                                    children: [
                                      Expanded(
                                        child: Text(
                                            '${item.index}. ${item.label}'),
                                      ),
                                      Text(
                                        _formatNumber(item.value),
                                        style: const TextStyle(
                                            fontWeight: FontWeight.w600),
                                      ),
                                    ],
                                  ),
                                ))
                            .toList(),
                      ),
                  ],
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }

  TableRow _row(String label, String value) {
    return TableRow(children: [
      Padding(
        padding: const EdgeInsets.symmetric(vertical: 4),
        child: Text(label),
      ),
      Padding(
        padding: const EdgeInsets.symmetric(vertical: 4),
        child: Text(value, style: const TextStyle(fontWeight: FontWeight.w600)),
      ),
    ]);
  }
}
