import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import '../api/api_client.dart';
import 'package:intl/intl.dart';
import '../api/models.dart';
import '../api/invoice_service.dart';
import '../api/expense_service.dart';
import '../api/pricing_service.dart';
import 'main_menu_page.dart';

class HomePage extends StatefulWidget {
  final ApiClient api;
  final Future<void> Function() onLogout;
  final int initialTab; // 0: Fatura, 1: Gider
  const HomePage({super.key, required this.api, required this.onLogout, this.initialTab = 0});

  @override
  State<HomePage> createState() => _HomePageState();
}

class _HomePageState extends State<HomePage> {
  PricingCurrent? _pricing;
  bool _loadingPricing = true;
  // Global karat alert state
  bool _hideAlert = false;
  double _diff22 = 0;
  double _diff24 = 0;
  double _alertThreshold = 1000;
  bool _loadingAlert = true;

  // Invoice form state
  final invName = TextEditingController();
  final invTckn = TextEditingController();
  final invTutar = TextEditingController();
  DateTime invDate = DateTime.now();
  OdemeSekli invOdeme = OdemeSekli.Havale;
  AltinAyar invAyar = AltinAyar.Ayar22;

  // Expense form state
  final expName = TextEditingController();
  final expTckn = TextEditingController();
  final expTutar = TextEditingController();
  DateTime expDate = DateTime.now();
  AltinAyar expAyar = AltinAyar.Ayar22;

  // Preview/draft state
  String? invDraftId;
  int? invPredictedSira;
  double? invAltinSatis;
  String? expDraftId;
  int? expPredictedSira;
  double? expAltinSatis;

  String? invResult;
  String? expResult;
  bool creatingInv = false;
  bool creatingExp = false;

  @override
  void initState() {
    super.initState();
    _refreshPricing();
    _refreshKaratAlert();
  }

  Future<void> _refreshPricing() async {
    setState(() => _loadingPricing = true);
    final p = await PricingService(widget.api).current();
    if (!mounted) return;
    setState(() {
      _pricing = p;
      _loadingPricing = false;
    });
  }

  Future<void> _refreshKaratAlert() async {
    setState(() { _loadingAlert = true; });
    try {
      // Load karat settings
      final cfg = await widget.api.getJson('/api/settings/karat');
      final thr = (cfg['alertThreshold'] as num?)?.toDouble() ?? 1000.0;
      // Load latest invoices/expenses page
      final inv = await widget.api.getJson('/api/invoices', query: { 'page': 1, 'pageSize': 500 });
      final exp = await widget.api.getJson('/api/expenses', query: { 'page': 1, 'pageSize': 500 });
      final itemsInv = (inv['items'] as List?) ?? const [];
      final itemsExp = (exp['items'] as List?) ?? const [];
      final now = DateTime.now();
      final monthKey = '${now.year.toString().padLeft(4,'0')}-${now.month.toString().padLeft(2,'0')}';
      double inv22 = 0, inv24 = 0, exp22 = 0, exp24 = 0;
      int toAyar(dynamic v) {
        if (v == 22 || v == 'Ayar22') return 22;
        if (v == 24 || v == 'Ayar24') return 24;
        return 0;
      }
      bool startsWithMonth(String? t) => t != null && t.startsWith(monthKey);
      for (final it in itemsInv) {
        final m = it as Map<String, dynamic>;
        if ((m['kesildi'] == true) && startsWithMonth(m['tarih'] as String?)) {
          final ayar = toAyar(m['altinAyar']);
          final g = (m['gramDegeri'] as num?)?.toDouble() ?? 0;
          if (ayar == 22) inv22 += g; else if (ayar == 24) inv24 += g;
        }
      }
      for (final it in itemsExp) {
        final m = it as Map<String, dynamic>;
        if ((m['kesildi'] == true) && startsWithMonth(m['tarih'] as String?)) {
          final ayar = toAyar(m['altinAyar']);
          final g = (m['gramDegeri'] as num?)?.toDouble() ?? 0;
          if (ayar == 22) exp22 += g; else if (ayar == 24) exp24 += g;
        }
      }
      if (!mounted) return;
      setState(() {
        _alertThreshold = thr;
        _diff22 = (inv22 - exp22);
        _diff24 = (inv24 - exp24);
        if (_diff22 < 0) _diff22 = 0;
        if (_diff24 < 0) _diff24 = 0;
        _loadingAlert = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() { _loadingAlert = false; });
    }
  }

  @override
  Widget build(BuildContext context) {
    return DefaultTabController(
      length: 2,
      initialIndex: widget.initialTab,
      child: Builder(
        builder: (ctx) {
          final tabController = DefaultTabController.of(ctx)!;
          return Scaffold(
            backgroundColor: Colors.grey[50],
            appBar: AppBar(
              title: AnimatedBuilder(
                animation: tabController.animation!,
                builder: (_, __) {
                  final idx = tabController.index;
                  return Text('Fatura / Gider - ' + (idx == 0 ? 'Fatura' : 'Gider'));
                },
              ),
              actions: [
                IconButton(
                  onPressed: () {
                    Navigator.of(context).push(
                      MaterialPageRoute(builder: (_) => MainMenuPage(api: widget.api, onLogout: widget.onLogout)),
                    );
                  },
                  icon: const Icon(Icons.home_outlined),
                  tooltip: 'Ana Ekran',
                ),
                IconButton(onPressed: _refreshPricing, icon: const Icon(Icons.refresh)),
                IconButton(
                  onPressed: () async {
                    await widget.onLogout();
                    if (mounted) Navigator.of(context).pop();
                  },
                  icon: const Icon(Icons.logout),
                  tooltip: 'Çıkış',
                ),
              ],
              bottom: const TabBar(
                tabs: [
                  Tab(text: 'Fatura'),
                  Tab(text: 'Gider'),
                ],
              ),
            ),
            body: TabBarView(
              children: [
                SingleChildScrollView(
                  padding: const EdgeInsets.all(16),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      _buildGlobalKaratAlert(),
                      const SizedBox(height: 12),
                      _buildPricingCard(),
                      const SizedBox(height: 12),
                      _invoiceFormTab(),
                    ],
                  ),
                ),
                SingleChildScrollView(
                  padding: const EdgeInsets.all(16),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      _buildGlobalKaratAlert(),
                      const SizedBox(height: 12),
                      _buildPricingCard(),
                      const SizedBox(height: 12),
                      _expenseFormTab(),
                    ],
                  ),
                ),
              ],
            ),
          );
        },
      ),
    );
  }

  Widget _buildPricingCard() {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: _loadingPricing
            ? const Row(children: [CircularProgressIndicator(), SizedBox(width: 12), Text('Fiyat yükleniyor...')])
            : (_pricing == null
                ? const Text('Güncel fiyat bulunamadı..')
                : Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                    const Text('ALTIN Son Fiyat'),
                    Text('${_fmtAmount(_pricing!.finalSatis)} TL/gr', style: Theme.of(context).textTheme.headlineSmall),
                    Text('Kaynak: ${_pricing!.sourceTime.toLocal()}')
                  ])),
      ),
    );
  }

  Widget _buildGlobalKaratAlert() {
    if (_hideAlert) return const SizedBox.shrink();
    if (_loadingAlert) return const SizedBox.shrink();
    final trig22 = _diff22 > _alertThreshold;
    final trig24 = _diff24 > _alertThreshold;
    if (!trig22 && !trig24) return const SizedBox.shrink();
    final parts = <String>[];
    if (trig22) parts.add('22 Ayar (${_fmtNumber(_diff22)} gr)');
    if (trig24) parts.add('24 Ayar (${_fmtNumber(_diff24)} gr)');
    final which = parts.join(' ve ');
    return Card(
      color: Colors.amber[100],
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(8), side: BorderSide(color: Colors.amber.shade200)),
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const Icon(Icons.warning_amber_rounded, color: Colors.amber),
            const SizedBox(width: 8),
            Expanded(
              child: Text(
                'Dikkat! $which için faturalanan altın ile gider altını arasında fark var. Gider kesiniz.',
                style: const TextStyle(color: Colors.black87, fontSize: 13),
              ),
            ),
            IconButton(
              tooltip: 'Gizle',
              onPressed: () => setState(() => _hideAlert = true),
              icon: const Icon(Icons.close, size: 18),
            ),
          ],
        ),
      ),
    );
  }

  String _fmtNumber(double v) {
    try { return NumberFormat.decimalPattern('tr_TR').format(v); } catch (_) { return v.toStringAsFixed(2); }
  }

  // --- Tabs ---
  Widget _invoiceFormTab() {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            _datePickerRow(
              label: 'Tarih',
              selectedDate: invDate,
              onPick: () async {
                final picked = await showDatePicker(
                  context: context,
                  initialDate: invDate,
                  firstDate: DateTime(2020),
                  lastDate: DateTime(2100),
                );
                if (picked != null) setState(() => invDate = picked);
              },
            ),
            const SizedBox(height: 8),
            TextField(
              controller: invName,
              decoration: const InputDecoration(labelText: 'Ad Soyad'),
              textCapitalization: TextCapitalization.characters,
              inputFormatters: const [TurkishUppercaseTextFormatter()],
            ),
            const SizedBox(height: 8),
            TextField(
              controller: invTckn,
              decoration: const InputDecoration(labelText: 'TCKN (11 hane)'),
              keyboardType: TextInputType.number,
              inputFormatters: [
                FilteringTextInputFormatter.digitsOnly,
                LengthLimitingTextInputFormatter(11),
              ],
            ),
            const SizedBox(height: 8),
            TextField(
              controller: invTutar,
              decoration: const InputDecoration(labelText: 'Tutar (TL)').copyWith(prefixText: '₺ '),
              keyboardType: TextInputType.number,
              inputFormatters: [
                FilteringTextInputFormatter.digitsOnly,
                const ThousandsSeparatorInputFormatter(),
              ],
            ),
            const SizedBox(height: 12),
            _ayarToggle(current: invAyar, onChanged: (v) => setState(() => invAyar = v)),
            const SizedBox(height: 12),
            _odemeToggle(current: invOdeme, onChanged: (v) => setState(() => invOdeme = v)),
            const SizedBox(height: 16),
            FilledButton.icon(
              onPressed: creatingInv ? null : _previewInvoice,
              icon: const Icon(Icons.visibility),
              label: Text(creatingInv ? 'Hazırlanıyor!' : 'Önizle'),
            ),
            if (invResult != null) ...[
              const SizedBox(height: 12),
              Text(invResult!, textAlign: TextAlign.center),
            ],
          ],
        ),
      ),
    );
  }

  Widget _expenseFormTab() {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            _datePickerRow(
              label: 'Tarih',
              selectedDate: expDate,
              onPick: () async {
                final picked = await showDatePicker(
                  context: context,
                  initialDate: expDate,
                  firstDate: DateTime(2020),
                  lastDate: DateTime(2100),
                );
                if (picked != null) setState(() => expDate = picked);
              },
            ),
            const SizedBox(height: 8),
            TextField(
              controller: expName,
              decoration: const InputDecoration(labelText: 'Ad Soyad'),
              textCapitalization: TextCapitalization.characters,
              inputFormatters: const [TurkishUppercaseTextFormatter()],
            ),
            const SizedBox(height: 8),
            TextField(controller: expTckn,decoration: const InputDecoration(labelText: 'TCKN (11 hane)'),keyboardType: TextInputType.number,inputFormatters: [FilteringTextInputFormatter.digitsOnly,LengthLimitingTextInputFormatter(11),],),
            const SizedBox(height: 8),
            TextField(controller: expTutar,decoration: const InputDecoration(labelText: 'Tutar (TL)').copyWith(prefixText: '₺ '),keyboardType: TextInputType.number,inputFormatters: [FilteringTextInputFormatter.digitsOnly,const ThousandsSeparatorInputFormatter(),],),
            const SizedBox(height: 12),
            _ayarToggle(current: expAyar, onChanged: (v) => setState(() => expAyar = v)),
            const SizedBox(height: 16),
            FilledButton.icon(
              onPressed: creatingExp ? null : _previewExpense,
              icon: const Icon(Icons.visibility),
              label: Text(creatingExp ? 'Hazırlanıyor..' : 'Önizle'),
            ),
            if (expResult != null) ...[
              const SizedBox(height: 12),
              Text(expResult!, textAlign: TextAlign.center),
            ],
          ],
        ),
      ),
    );
  }

  // --- Shared widgets ---
  Widget _datePickerRow({
    required String label,
    required DateTime selectedDate,
    required Future<void> Function() onPick,
  }) {
    return Row(
      children: [
        Expanded(
          child: InputDecorator(
            decoration: InputDecoration(labelText: label),
            child: Text(_formatDate(selectedDate)),
          ),
        ),
        const SizedBox(width: 8),
        OutlinedButton.icon(
          onPressed: onPick,
          icon: const Icon(Icons.date_range),
          label: const Text('Seç'),
        ),
      ],
    );
  }

  String _formatDate(DateTime d) =>
      '${d.day.toString().padLeft(2, '0')}.${d.month.toString().padLeft(2, '0')}.${d.year}';

  Widget _ayarToggle({
    required AltinAyar current,
    required ValueChanged<AltinAyar> onChanged,
  }) {
    final is22 = current == AltinAyar.Ayar22;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const Text('Ayar'),
        const SizedBox(height: 4),
        Center(
          child: ToggleButtons(
            isSelected: [is22, !is22],
            onPressed: (i) => onChanged(i == 0 ? AltinAyar.Ayar22 : AltinAyar.Ayar24),
            children: const [
              Padding(padding: EdgeInsets.symmetric(horizontal: 16), child: Text('22 Ayar')),
              Padding(padding: EdgeInsets.symmetric(horizontal: 16), child: Text('24 Ayar')),
            ],
          ),
        ),
      ],
    );
  }

  Widget _odemeToggle({
    required OdemeSekli current,
    required ValueChanged<OdemeSekli> onChanged,
  }) {
    final isHavale = current == OdemeSekli.Havale;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const Text('Ödeme Tipi'),
        const SizedBox(height: 4),
        Center(
          child: ToggleButtons(
            isSelected: [isHavale, !isHavale],
            onPressed: (i) => onChanged(i == 0 ? OdemeSekli.Havale : OdemeSekli.KrediKarti),
            children: const [
              Padding(padding: EdgeInsets.symmetric(horizontal: 16), child: Text('Havale')),
              Padding(padding: EdgeInsets.symmetric(horizontal: 16), child: Text('Kredi Kartı')),
            ],
          ),
        ),
      ],
    );
  }

  // --- Preview flows (server-backed, like web cashier) ---
  Future<void> _previewInvoice() async {
    final name = invName.text.trim();
    final tckn = invTckn.text.trim();
    final tutar = _parseAmount(invTutar.text);
    if (name.isEmpty || tckn.isEmpty || tutar == null) { _showError('Lütfen tüm alanları doldurun'); return; }
    if (!_validateTckn(tckn)) { _showError('TCKN geçersiz!'); return; }
    try {
      setState(() { creatingInv = true; invResult = null; });
      final dto = CreateInvoiceDto(
        tarih: invDate,
        musteriAdSoyad: name,
        tckn: tckn,
        tutar: tutar,
        odemeSekli: invOdeme,
        altinAyar: invAyar,
      );
      final created = await InvoiceService(widget.api).createDraft(dto);
      setState(() { invDraftId = created.id; invPredictedSira = created.siraNo; invAltinSatis = created.altinSatisFiyati; });
      await _showInvoicePreviewDialog();
    } catch (e) {
      _showError('Önizleme oluşturulamadı!');
    } finally {
      if (mounted) setState(() { creatingInv = false; });
    }
  }

  Future<void> _previewExpense() async {
    final name = expName.text.trim();
    final tckn = expTckn.text.trim();
    final tutar = _parseAmount(expTutar.text);
    if (name.isEmpty || tckn.isEmpty || tutar == null) { _showError('Lütfen tüm alanları doldurun'); return; }
    if (!_validateTckn(tckn)) { _showError('TCKN geçersiz'); return; }
    try {
      setState(() { creatingExp = true; expResult = null; });
      final dto = CreateExpenseDto(
        tarih: expDate,
        musteriAdSoyad: name,
        tckn: tckn,
        tutar: tutar,
        altinAyar: expAyar,
      );
      final created = await ExpenseService(widget.api).createDraft(dto);
      setState(() { expDraftId = created.id; expPredictedSira = created.siraNo; expAltinSatis = created.altinSatisFiyati; });
      await _showExpensePreviewDialog();
    } catch (e) {
      _showError('Önizleme oluşturulamadı!');
    } finally {
      if (mounted) setState(() { creatingExp = false; });
    }
  }

  bool _validateTckn(String id) {
    if (!RegExp(r'^\d{11}$').hasMatch(id)) return false;
    if (id.startsWith('0')) return false;
    final digits = id.split('').take(11).map((e) => int.tryParse(e) ?? 0).toList();
    final calc10 = (((digits[0] + digits[2] + digits[4] + digits[6] + digits[8]) * 7) - (digits[1] + digits[3] + digits[5] + digits[7])) % 10;
    final calc11 = (digits.take(10).reduce((a, b) => a + b)) % 10;
    return digits[9] == calc10 && digits[10] == calc11;
  }

  Future<void> _showInvoicePreviewDialog() async {
    final name = invName.text.trim();
    final tckn = invTckn.text.trim();
    final tutar = _parseAmount(invTutar.text) ?? 0;
    return showDialog(
      context: context,
      barrierDismissible: false,
      builder: (_) => AlertDialog(
        title: const Text('Fatura Önizleme'),
        content: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            if (invPredictedSira != null) Text('Sıra No: $invPredictedSira'),
            if (invAltinSatis != null) Text('Altın Satış Fiyatı: ${_fmtAmount(invAltinSatis!)} TL/gr'),
            const SizedBox(height: 8),
            Text('Tarih: ${_formatDate(invDate)}'),
            Text('Ad Soyad: $name'),
            Text('TCKN: $tckn'),
            Text('Tutar: ${_fmtAmount(tutar)} TL'),
            Text('Ayar: ${invAyar == AltinAyar.Ayar22 ? '22 Ayar' : '24 Ayar'}'),
            _buildDerivedPreview(tutar, invAltinSatis, invAyar),
            Text('Ödeme: ${invOdeme == OdemeSekli.Havale ? 'Havale' : 'Kredi Kartı'}'),
          ],
        ),
        actions: [
          TextButton(onPressed: () async {
            final id = invDraftId; // cancel preview and delete draft
            Navigator.of(context).pop();
            if (id != null) { try { await InvoiceService(widget.api).deleteIfNotKesildi(id); } catch (_) {} }
            setState(() { invDraftId = null; invPredictedSira = null; invAltinSatis = null; });
          }, child: const Text('İptal')),
          FilledButton(onPressed: () async {
            final id = invDraftId;
            if (id == null) return;
            try {
              await InvoiceService(widget.api).finalize(id);
              if (!mounted) return;
              setState(() { invResult = 'Kesildi: Sıra No $invPredictedSira'; });
              Navigator.of(context).pop();
              setState(() { invDraftId = null; invPredictedSira = null; invAltinSatis = null; });
            } catch (e) {
              _showError('Fatura kesilemedi');
            }
          }, child: const Text('Kes')),
        ],
      ),
    );
  }

  Future<void> _showExpensePreviewDialog() async {
    final name = expName.text.trim();
    final tckn = expTckn.text.trim();
    final tutar = _parseAmount(expTutar.text) ?? 0;
    return showDialog(
      context: context,
      barrierDismissible: false,
      builder: (_) => AlertDialog(
        title: const Text('Gider Önizleme'),
        content: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            if (expPredictedSira != null) Text('Sıra No: $expPredictedSira'),
            if (expAltinSatis != null) Text('Altın Satış: ${_fmtAmount(expAltinSatis!)} TL/gr'),
            const SizedBox(height: 8),
            Text('Tarih: ${_formatDate(expDate)}'),
            Text('Ad Soyad: $name'),
            Text('TCKN: $tckn'),
            Text('Tutar: ${_fmtAmount(tutar)} TL'),
            Text('Ayar: ${expAyar == AltinAyar.Ayar22 ? '22 Ayar' : '24 Ayar'}'),
            _buildDerivedPreview(tutar, expAltinSatis, expAyar),
          ],
        ),
        actions: [
          TextButton(onPressed: () async {
            final id = expDraftId;
            Navigator.of(context).pop();
            if (id != null) { try { await ExpenseService(widget.api).deleteIfNotKesildi(id); } catch (_) {} }
            setState(() { expDraftId = null; expPredictedSira = null; expAltinSatis = null; });
          }, child: const Text('İptal')),
          FilledButton(onPressed: () async {
            final id = expDraftId;
            if (id == null) return;
            try {
              await ExpenseService(widget.api).finalize(id);
              if (!mounted) return;
              setState(() { expResult = 'Kesildi: Sıra No $expPredictedSira'; });
              Navigator.of(context).pop();
              setState(() { expDraftId = null; expPredictedSira = null; expAltinSatis = null; });
            } catch (e) {
              _showError('Gider kesilemedi');
            }
          }, child: const Text('Kes')),
        ],
      ),
    );
  }

  void _showError(String msg) {
    ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(msg)));
  }

  String _fmtAmount(num v, {int fractionDigits = 0}) {
    final f = NumberFormat.decimalPattern('tr_TR');
    f.minimumFractionDigits = fractionDigits;
    f.maximumFractionDigits = fractionDigits;
    return f.format(v);
  }

  double? _parseAmount(String s) {
    final cleaned = s.replaceAll('.', '').replaceAll(',', '.');
    return double.tryParse(cleaned);
  }

  // Compute and render derived preview metrics (saf altın, yeni ürün, gram, işçilik)
  Widget _buildDerivedPreview(double tutar, double? altinSatis, AltinAyar ayar) {
    if (altinSatis == null) return const SizedBox.shrink();
    double r2(double x) => (x * 100).round() / 100.0;
    final safAltin = r2(ayar == AltinAyar.Ayar22 ? altinSatis * 0.916 : altinSatis * 0.995);
    final yeniUrun = r2(ayar == AltinAyar.Ayar22 ? tutar * 0.99 : tutar * 0.998);
    final gram = safAltin == 0 ? 0.0 : r2(yeniUrun / safAltin);
    final altinHizmet = r2(gram * safAltin);
    final iscilikKdvli = r2(r2(tutar) - altinHizmet);
    final iscilik = r2(iscilikKdvli / 1.20);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const SizedBox(height: 8),
        Text('Saf Altın Değeri: ${_fmtAmount(safAltin, fractionDigits: 2)} TL/gr'),
        Text('Yeni Ürün Fiyatı: ${_fmtAmount(yeniUrun, fractionDigits: 2)} TL'),
        Text('Gram Değeri: ${_fmtAmount(gram, fractionDigits: 2)} gr'),
        Text('İşçilik (KDV\'siz): ${_fmtAmount(iscilik, fractionDigits: 2)} TL'),
      ],
    );
  }
}

class ThousandsSeparatorInputFormatter extends TextInputFormatter {
  const ThousandsSeparatorInputFormatter();

  @override
  TextEditingValue formatEditUpdate(TextEditingValue oldValue, TextEditingValue newValue) {
    final rawDigits = newValue.text.replaceAll(RegExp(r'[^0-9]'), '');
    if (rawDigits.isEmpty) {
      return const TextEditingValue(text: '', selection: TextSelection.collapsed(offset: 0));
    }
    final number = int.parse(rawDigits);
    final formatted = NumberFormat.decimalPattern('tr_TR')
      ..minimumFractionDigits = 0
      ..maximumFractionDigits = 0;
    final newText = formatted.format(number);

    final cursorFromRight = newValue.text.length - newValue.selection.extentOffset;
    var newOffset = newText.length - cursorFromRight;
    if (newOffset < 0) newOffset = 0;
    if (newOffset > newText.length) newOffset = newText.length;
    return TextEditingValue(text: newText, selection: TextSelection.collapsed(offset: newOffset));
  }
}

class TurkishUppercaseTextFormatter extends TextInputFormatter {
  const TurkishUppercaseTextFormatter();

  String _trUpper(String s) {
    final buf = StringBuffer();
    for (final ch in s.split('')) {
      if (ch == 'i') {
        buf.write('İ');
      } else if (ch == 'ı') {
        buf.write('I');
      } else {
        buf.write(ch.toUpperCase());
      }
    }
    return buf.toString();
  }

  @override
  TextEditingValue formatEditUpdate(TextEditingValue oldValue, TextEditingValue newValue) {
    final up = _trUpper(newValue.text);
    final sel = newValue.selection;
    final offset = sel.isValid ? sel.baseOffset : up.length;
    final safeOffset = offset.clamp(0, up.length);
    return TextEditingValue(text: up, selection: TextSelection.collapsed(offset: safeOffset));
  }
}
