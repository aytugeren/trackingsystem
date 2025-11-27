import 'dart:async';
import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;
import 'package:intl/intl.dart';
import '../api/api_client.dart';
import '../api/settings_service.dart';

class GoldItem {
  final String code;
  final double alis;
  final double satis;
  final String tarih;
  final String alisDir;
  final String satisDir;

  GoldItem({
    required this.code,
    required this.alis,
    required this.satis,
    required this.tarih,
    required this.alisDir,
    required this.satisDir,
  });

  factory GoldItem.fromJson(String key, Map<String, dynamic> json) {
    return GoldItem(
      code: key,
      alis: double.tryParse(json["alis"].toString()) ?? 0,
      satis: double.tryParse(json["satis"].toString()) ?? 0,
      tarih: json["tarih"]?.toString() ?? "",
      alisDir: (json["dir"]?["alis_dir"])?.toString() ?? "",
      satisDir: (json["dir"]?["satis_dir"])?.toString() ?? "",
    );
  }
}

class GoldPriceScreen extends StatefulWidget {
  final ApiClient api;
  const GoldPriceScreen({super.key, required this.api});

  @override
  State<GoldPriceScreen> createState() => _GoldPriceScreenState();
}

class _GoldPriceScreenState extends State<GoldPriceScreen>
    with SingleTickerProviderStateMixin {
  static const endpoint = 'https://canlipiyasalar.haremaltin.com/tmp/altin.json';
  Future<_GoldPayload>? _future;
  Timer? _timer;
  final http.Client _client = http.Client();
  bool _refreshing = false;
  DateTime? _lastStatusCheck;
  String? _statusMessage;

  @override
  void initState() {
    super.initState();
    _future = _load();
    _timer = Timer.periodic(const Duration(seconds: 15), (_) {
      if (!mounted || _refreshing) return;
      setState(() {
        _future = _load();
      });
    });
  }

  @override
  void dispose() {
    _timer?.cancel();
    _client.close();
    super.dispose();
  }

  Future<_GoldPayload> _load() async {
    if (_refreshing) {
      return _future ?? Future.error('Yenileme devam ediyor');
    }
    _refreshing = true;
    try {
      await _refreshStatusIfNeeded();
      final settingsSvc = SettingsService(widget.api);
      final calc = await settingsSvc.getCalcSettings();
      final resp = await _client.get(Uri.parse(endpoint)).timeout(const Duration(seconds: 10));
      if (resp.statusCode < 200 || resp.statusCode >= 300) {
        throw Exception('HTTP ${resp.statusCode}');
      }
      final json = jsonDecode(utf8.decode(resp.bodyBytes)) as Map<String, dynamic>;
      final metaTarih = json['meta']?['tarih']?.toString() ?? '';
      final data = (json['data'] as Map<String, dynamic>);
      final items = <GoldItem>[];
      for (final e in data.entries) {
        if (e.value is Map<String, dynamic>) {
          final raw = GoldItem.fromJson(e.key, e.value as Map<String, dynamic>);
          double satis = raw.satis;

          if (calc.defaultKariHesapla) {
            final margin = 1.0 + (calc.karMargin / 100.0);
            satis *= margin;
          }

          if (calc.includeTax) {
            satis *= (1.0 + (calc.taxRate / 100.0));
          }

          items.add(GoldItem(
            code: raw.code,
            alis: raw.alis,
            satis: satis,
            tarih: raw.tarih,
            alisDir: raw.alisDir,
            satisDir: raw.satisDir,
          ));
        }
      }

      return _GoldPayload(metaTarih: metaTarih, items: items, precision: calc.decimalPrecision);
    } finally {
      _refreshing = false;
    }
  }

  Future<void> _refreshStatusIfNeeded() async {
    final now = DateTime.now();
    if (_lastStatusCheck != null && now.difference(_lastStatusCheck!) < const Duration(minutes: 1)) return;
    const warning = 'Şu anda güncel fiyatları çekilemiyor.';
    try {
      final status = await widget.api.getJson('/api/pricing/status');
      if (!mounted) return;
      setState(() {
        _statusMessage = status['hasAlert'] == true ? warning : null;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _statusMessage = warning;
      });
    }
    _lastStatusCheck = now;
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: Colors.grey[50],
      appBar: AppBar(title: const Text('İstanbul Kapalıçarşı Fiyatları')),
      body: Column(
        children: [
          if (_statusMessage != null)
            Container(
              width: double.infinity,
              color: Colors.red.shade50,
              padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 10),
              child: Text(
                _statusMessage!,
                style: const TextStyle(fontSize: 13, fontWeight: FontWeight.w600, color: Colors.red),
              ),
            ),
          Expanded(
            child: RefreshIndicator(
        onRefresh: () async { setState(() { _future = _load(); }); await _future; },
        child: FutureBuilder<_GoldPayload>(
          future: _future,
          builder: (context, snap) {
            if (snap.connectionState == ConnectionState.waiting) {
              return const Center(child: CircularProgressIndicator());
            }
            if (snap.hasError) {
              return const Center(child: Text('Bağlantı hatası'));
            }
            final payload = snap.data!;
            final items = payload.items;
            final f2 = NumberFormat.decimalPattern('tr_TR')
              ..minimumFractionDigits = payload.precision
              ..maximumFractionDigits = payload.precision;

            List<String> topCodes = [
              'ALTIN', 'AYAR22', 'AYAR14', 'KULCEALTIN', 'ONS', 'USDTRY', 'EURTRY'
            ];
            List<String> ziynetCodes = [
              'CEYREK_YENI', 'YARIM_YENI', 'TEK_YENI', 'ATA_YENI', 'ATA5_YENI'
            ];
            String label(String code) {
              const map = {
                'ALTIN': 'Altın',
                'AYAR22': '22 Ayar',
                'AYAR14': '14 Ayar',
                'KULCEALTIN': 'Külçe',
                'ONS': 'ONS',
                'USDTRY': 'USD/TRY',
                'EURTRY': 'EUR/TRY',
                'GBPTRY': 'GBP/TRY',
                'CEYREK_YENI': 'Çeyrek (Yeni)',
                'YARIM_YENI': 'Yarım (Yeni)',
                'TEK_YENI': 'Tam (Yeni)',
                'ATA_YENI': 'Ata (Yeni)',
                'ATA5_YENI': '5xAta (Yeni)',
              };
              return map[code] ?? code;
            }

            GoldItem? find(String code) => items.firstWhere(
                  (x) => x.code.toUpperCase() == code.toUpperCase(),
                  orElse: () => GoldItem(
                    code: code,
                    alis: 0,
                    satis: 0,
                    tarih: payload.metaTarih,
                    alisDir: '',
                    satisDir: '',
                  ),
                );

            Color? dirColor(String dir) => dir == 'up' ? Colors.green : dir == 'down' ? Colors.red : null;

            Widget cardFor(GoldItem it) {
              return Container(
                decoration: BoxDecoration(
                  color: Colors.white,
                  borderRadius: BorderRadius.circular(12),
                  boxShadow: [BoxShadow(color: Colors.black12, blurRadius: 6, offset: const Offset(0, 2))],
                ),
                padding: const EdgeInsets.all(12),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(label(it.code), style: const TextStyle(fontWeight: FontWeight.w600)),
                    const SizedBox(height: 8),
                    Row(children: [
                      const Text('Alış: '),
                      Text('${f2.format(it.alis)} ₺', style: const TextStyle(fontWeight: FontWeight.w500)),
                      const SizedBox(width: 4),
                      if (it.alisDir.isNotEmpty) Icon(
                        it.alisDir == 'up' ? Icons.arrow_drop_up : Icons.arrow_drop_down,
                        color: dirColor(it.alisDir),
                      ),
                    ]),
                    Row(children: [
                      const Text('Satış: '),
                      Text('${f2.format(it.satis)} ₺', style: const TextStyle(fontWeight: FontWeight.w500)),
                      const SizedBox(width: 4),
                      if (it.satisDir.isNotEmpty) Icon(
                        it.satisDir == 'up' ? Icons.arrow_drop_up : Icons.arrow_drop_down,
                        color: dirColor(it.satisDir),
                      ),
                    ]),
                  ],
                ),
              );
            }

            final topList = topCodes.map((c) => find(c)!).toList();
            final ziynetList = ziynetCodes.map((c) => find(c)!).toList();
            final marqueeItems = [
              'USDTRY', 'EURTRY', 'GBPTRY', 'ONS'
            ].map((c) => find(c)!).where((e) => e != null).cast<GoldItem>().toList();
            final marqueeText = marqueeItems.map((e) => '${label(e.code)}: ${f2.format(e.satis)}').join('   •   ');

            return ListView(
              padding: const EdgeInsets.all(16),
              children: [
                Row(
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  children: [
                    Text('Son Güncelleme: ${payload.metaTarih}', style: const TextStyle(color: Colors.black54)),
                    IconButton(onPressed: () { setState(() { _future = _load(); }); }, icon: const Icon(Icons.refresh))
                  ],
                ),
                const SizedBox(height: 8),
                // Top grid
                LayoutBuilder(builder: (context, c) {
                  final twoCol = c.maxWidth > 480;
                  return Wrap(
                    spacing: 12,
                    runSpacing: 12,
                    children: topList.map((e) => SizedBox(
                      width: twoCol ? (c.maxWidth - 12) / 2 : c.maxWidth,
                      child: cardFor(e),
                    )).toList(),
                  );
                }),
                const SizedBox(height: 16),
                const Text('Ziynet Türleri', style: TextStyle(fontSize: 16, fontWeight: FontWeight.w600)),
                const SizedBox(height: 8),
                ...ziynetList.map((e) => Padding(
                  padding: const EdgeInsets.only(bottom: 8),
                  child: cardFor(e),
                )),
                const SizedBox(height: 16),
                const Text('Döviz Kurları', style: TextStyle(fontSize: 16, fontWeight: FontWeight.w600)),
                const SizedBox(height: 8),
                _Marquee(text: marqueeText),
              ],
            );
          },
        ),
      ),
    ),
  ],
),
    );
  }
}

class _GoldPayload {
  final String metaTarih;
  final List<GoldItem> items;
  final int precision;
  _GoldPayload({required this.metaTarih, required this.items, required this.precision});
}

class _Marquee extends StatefulWidget {
  final String text;
  const _Marquee({required this.text});
  @override
  State<_Marquee> createState() => _MarqueeState();
}

class _MarqueeState extends State<_Marquee> with SingleTickerProviderStateMixin {
  late final AnimationController _c;
  double _width = 0;
  double _textWidth = 0;

  @override
  void initState() {
    super.initState();
    _c = AnimationController(vsync: this, duration: const Duration(seconds: 12))..repeat();
  }

  @override
  void dispose() {
    _c.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final style = const TextStyle(fontSize: 13, color: Colors.black87);
    return LayoutBuilder(builder: (context, cons) {
      _width = cons.maxWidth;
      return ClipRect(
        child: AnimatedBuilder(
          animation: _c,
          builder: (_, __) {
            final pos = (_c.value) * (_textWidth + 40);
            final dx = _width - (pos % (_textWidth + 40));
            return Stack(
              children: [
                Transform.translate(
                  offset: Offset(dx, 0),
                  child: _measureText(style),
                ),
                Transform.translate(
                  offset: Offset(dx + _textWidth + 40, 0),
                  child: _measureText(style),
                ),
              ],
            );
          },
        ),
      );
    });
  }

  Widget _measureText(TextStyle style) {
    return Builder(builder: (context) {
      final span = TextSpan(text: widget.text, style: style);
      final dir = Directionality.of(context);
      final tp = TextPainter(text: span, textDirection: dir)..layout();
      _textWidth = tp.size.width;
      return SizedBox(width: _textWidth, child: Text(widget.text, style: style));
    });
  }
}


