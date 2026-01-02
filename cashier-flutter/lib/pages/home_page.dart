import 'dart:async';
import 'dart:math' as Math;
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import '../api/api_client.dart';
import 'package:intl/intl.dart';
import '../api/models.dart';
import '../api/invoice_service.dart';
import '../api/expense_service.dart';
import '../api/pricing_service.dart';
import 'main_menu_page.dart';

enum SuggestSource { name, tckn, vkn }

class CustomerSuggestion {
  final String id;
  final String adSoyad;
  final String tckn;
  final bool isCompany;
  final String? vknNo;
  final String? companyName;
  final String? phone;
  final String? email;
  final bool hasContact;

  const CustomerSuggestion({
    required this.id,
    required this.adSoyad,
    required this.tckn,
    required this.isCompany,
    this.vknNo,
    this.companyName,
    this.phone,
    this.email,
    required this.hasContact,
  });
}

class HomePage extends StatefulWidget {
  final ApiClient api;
  final Future<void> Function() onLogout;
  final int initialTab; // 0: Fatura, 1: Gider
  const HomePage(
      {super.key,
      required this.api,
      required this.onLogout,
      this.initialTab = 0});

  @override
  State<HomePage> createState() => _HomePageState();
}

class _HomePageState extends State<HomePage> {
  GoldPrice? _goldPrice;
  bool _loadingPricing = true;
  bool _editingGold = false;
  bool _savingGold = false;
  final TextEditingController _goldController = TextEditingController();
  // Global karat alert state
  bool _hideAlert = false;
  double _diff22 = 0;
  double _diff24 = 0;
  double _alertThreshold = 1000;
  bool _loadingAlert = true;

  // Invoice form state
  final invName = TextEditingController();
  final invTckn = TextEditingController();
  final invVkn = TextEditingController();
  final invCompanyName = TextEditingController();
  bool invIsCompany = false;
  bool invIsForCompany = false;
  bool invCompanyChoiceTouched = false;
  final invTutar = TextEditingController();
  final invPhone = TextEditingController();
  final invEmail = TextEditingController();
  DateTime invDate = DateTime.now();
  OdemeSekli invOdeme = OdemeSekli.Havale;
  AltinAyar invAyar = AltinAyar.Ayar22;
  List<CustomerSuggestion> _invSuggestions = [];
  Timer? _invSuggestTimer;
  bool _invNeedsContact = false;

  // Expense form state
  final expName = TextEditingController();
  final expTckn = TextEditingController();
  final expVkn = TextEditingController();
  final expCompanyName = TextEditingController();
  bool expIsCompany = false;
  bool expIsForCompany = false;
  bool expCompanyChoiceTouched = false;
  final expTutar = TextEditingController();
  final expPhone = TextEditingController();
  final expEmail = TextEditingController();
  DateTime expDate = DateTime.now();
  AltinAyar expAyar = AltinAyar.Ayar22;
  List<CustomerSuggestion> _expSuggestions = [];
  Timer? _expSuggestTimer;
  bool _expNeedsContact = false;

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

  @override
  void dispose() {
    invName.dispose();
    invTckn.dispose();
    invVkn.dispose();
    invCompanyName.dispose();
    invTutar.dispose();
    invPhone.dispose();
    invEmail.dispose();
    expName.dispose();
    expTckn.dispose();
    expVkn.dispose();
    expCompanyName.dispose();
    expTutar.dispose();
    expPhone.dispose();
    expEmail.dispose();
    _goldController.dispose();
    _invSuggestTimer?.cancel();
    _expSuggestTimer?.cancel();
    super.dispose();
  }

  Future<List<CustomerSuggestion>> _fetchSuggestions(String query) async {
    try {
      final res = await widget.api.getJsonAny('/api/customers/suggest',
          query: {'q': query, 'limit': 8});
      final items = res is List
          ? res
          : (res is Map && res['items'] is List ? res['items'] as List : []);
      return items.map((it) {
        final map = it is Map ? it : <String, dynamic>{};
        final phoneVal = map['phone'] ?? map['Phone'];
        final emailVal = map['email'] ?? map['Email'];
        final hasContactVal =
            map['hasContact'] ?? map['has_contact'] ?? map['has_contact_info'];
        final isCompanyVal = map['isCompany'] ?? map['IsCompany'];
        final vknVal = map['vknNo'] ?? map['VknNo'];
        final companyVal = map['companyName'] ?? map['CompanyName'];
        final hasContact = hasContactVal is bool
            ? hasContactVal
            : ((phoneVal != null && '$phoneVal'.trim().isNotEmpty) ||
                (emailVal != null && '$emailVal'.trim().isNotEmpty));
        final id = '${map['id'] ?? map['Id'] ?? ''}'.trim();
        return CustomerSuggestion(
          id: id,
          adSoyad: '${map['adSoyad'] ?? map['AdSoyad'] ?? map['name'] ?? ''}'
              .trim(),
          tckn: '${map['tckn'] ?? map['TCKN'] ?? ''}'.trim(),
          isCompany: isCompanyVal == true,
          vknNo: vknVal == null ? null : '$vknVal'.trim(),
          companyName: companyVal == null ? null : '$companyVal'.trim(),
          phone: phoneVal == null ? null : '$phoneVal'.trim(),
          email: emailVal == null ? null : '$emailVal'.trim(),
          hasContact: hasContact,
        );
      }).where((s) => s.id.isNotEmpty).toList();
    } catch (_) {
      return [];
    }
  }

  void _scheduleInvoiceSuggest(SuggestSource source, String value) {
    final query = value.trim();
    _invSuggestTimer?.cancel();
    if (query.length < 2) {
      setState(() => _invSuggestions = []);
      return;
    }
    _invSuggestTimer = Timer(const Duration(milliseconds: 200), () async {
      final current = source == SuggestSource.name
          ? invName.text.trim()
          : (source == SuggestSource.tckn
              ? invTckn.text.trim()
              : invVkn.text.trim());
      if (current != query) return;
      final items = await _fetchSuggestions(query);
      if (!mounted) return;
      final stillCurrent = source == SuggestSource.name
          ? invName.text.trim() == query
          : (source == SuggestSource.tckn
              ? invTckn.text.trim() == query
              : invVkn.text.trim() == query);
      if (!stillCurrent) return;
      setState(() => _invSuggestions = items);
    });
  }

  void _scheduleExpenseSuggest(SuggestSource source, String value) {
    final query = value.trim();
    _expSuggestTimer?.cancel();
    if (query.length < 2) {
      setState(() => _expSuggestions = []);
      return;
    }
    _expSuggestTimer = Timer(const Duration(milliseconds: 200), () async {
      final current = source == SuggestSource.name
          ? expName.text.trim()
          : (source == SuggestSource.tckn
              ? expTckn.text.trim()
              : expVkn.text.trim());
      if (current != query) return;
      final items = await _fetchSuggestions(query);
      if (!mounted) return;
      final stillCurrent = source == SuggestSource.name
          ? expName.text.trim() == query
          : (source == SuggestSource.tckn
              ? expTckn.text.trim() == query
              : expVkn.text.trim() == query);
      if (!stillCurrent) return;
      setState(() => _expSuggestions = items);
    });
  }

  void _applyInvoiceSuggestion(CustomerSuggestion s) {
    setState(() {
      invName.text = s.adSoyad;
      invTckn.text = s.tckn;
      invIsCompany = s.isCompany;
      invVkn.text = s.vknNo ?? '';
      invCompanyName.text = s.companyName ?? '';
      invIsForCompany = (s.vknNo ?? '').trim().isNotEmpty;
      invCompanyChoiceTouched = false;
      invPhone.text = s.phone ?? '';
      invEmail.text = s.email ?? '';
      _invNeedsContact =
          !s.hasContact && invPhone.text.trim().isEmpty && invEmail.text.isEmpty;
      _invSuggestions = [];
    });
  }

  void _applyExpenseSuggestion(CustomerSuggestion s) {
    setState(() {
      expName.text = s.adSoyad;
      expTckn.text = s.tckn;
      expIsCompany = s.isCompany;
      expVkn.text = s.vknNo ?? '';
      expCompanyName.text = s.companyName ?? '';
      expIsForCompany = (s.vknNo ?? '').trim().isNotEmpty;
      expCompanyChoiceTouched = false;
      expPhone.text = s.phone ?? '';
      expEmail.text = s.email ?? '';
      _expNeedsContact =
          !s.hasContact && expPhone.text.trim().isEmpty && expEmail.text.isEmpty;
      _expSuggestions = [];
    });
  }

  Widget _suggestionList(
      List<CustomerSuggestion> items, void Function(CustomerSuggestion) onTap) {
    if (items.isEmpty) return const SizedBox.shrink();
    return Container(
      margin: const EdgeInsets.only(top: 6),
      decoration: BoxDecoration(
        color: Colors.white,
        border: Border.all(color: Colors.grey.shade300),
        borderRadius: BorderRadius.circular(8),
      ),
      child: Column(
        children: [
          for (var i = 0; i < items.length; i++) ...[
            InkWell(
              onTap: () => onTap(items[i]),
              child: Padding(
                padding:
                    const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
                child: Row(
                  children: [
                    const Icon(Icons.person, size: 18, color: Colors.black54),
                    const SizedBox(width: 8),
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(items[i].adSoyad,
                              style:
                                  const TextStyle(fontWeight: FontWeight.w600)),
                          if ((items[i].companyName ?? '').isNotEmpty)
                            Text('Şirket: ${items[i].companyName}',
                                style: const TextStyle(
                                    fontSize: 12, color: Colors.black54)),
                          if (items[i].tckn.isNotEmpty)
                            Text('TCKN: ${items[i].tckn}',
                                style: const TextStyle(
                                    fontSize: 12, color: Colors.black54)),
                          if (items[i].isCompany &&
                              (items[i].vknNo ?? '').isNotEmpty)
                            Text('VKN: ${items[i].vknNo}',
                                style: const TextStyle(
                                    fontSize: 12, color: Colors.black54)),
                        ],
                      ),
                    ),
                  ],
                ),
              ),
            ),
            if (i != items.length - 1)
              Divider(height: 1, color: Colors.grey.shade200),
          ],
        ],
      ),
    );
  }

  Future<void> _refreshPricing() async {
    setState(() => _loadingPricing = true);
    final p = await PricingService(widget.api).gold();
    if (!mounted) return;
    setState(() {
      _goldPrice = p;
      _loadingPricing = false;
      if (_editingGold && p != null) {
        _goldController.text = p.price.toStringAsFixed(3);
      }
    });
  }

  Future<void> _refreshKaratAlert() async {
    setState(() {
      _loadingAlert = true;
    });
    try {
      // Load karat settings
      final cfg = await widget.api.getJson('/api/settings/karat');
      final thr = (cfg['alertThreshold'] as num?)?.toDouble() ?? 1000.0;
      // Load latest invoices/expenses page
      final inv = await widget.api
          .getJson('/api/invoices', query: {'page': 1, 'pageSize': 500});
      final exp = await widget.api
          .getJson('/api/expenses', query: {'page': 1, 'pageSize': 500});
      final itemsInv = (inv['items'] as List?) ?? const [];
      final itemsExp = (exp['items'] as List?) ?? const [];
      final now = DateTime.now();
      final monthKey =
          '${now.year.toString().padLeft(4, '0')}-${now.month.toString().padLeft(2, '0')}';
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
          if (ayar == 22) {
            inv22 += g;
          } else if (ayar == 24) {
            inv24 += g;
          }
        }
      }
      for (final it in itemsExp) {
        final m = it as Map<String, dynamic>;
        if ((m['kesildi'] == true) && startsWithMonth(m['tarih'] as String?)) {
          final ayar = toAyar(m['altinAyar']);
          final g = (m['gramDegeri'] as num?)?.toDouble() ?? 0;
          if (ayar == 22) {
            exp22 += g;
          } else if (ayar == 24) {
            exp24 += g;
          }
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
      setState(() {
        _loadingAlert = false;
      });
    }
  }

  Future<void> _saveGoldPrice() async {
    final parsed = _parseDecimal(_goldController.text);
    if (parsed == null || parsed <= 0) {
      _showError('Gecerli bir has altin fiyati girin');
      return;
    }
    setState(() => _savingGold = true);
    try {
      final updated = await PricingService(widget.api).updateGold(parsed);
      if (!mounted) return;
      setState(() {
        _goldPrice = updated;
        _editingGold = false;
      });
    } catch (e) {
      _showError(_formatApiError(e, 'Has altin fiyati kaydedilemedi'));
    } finally {
      if (mounted) setState(() => _savingGold = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return DefaultTabController(
      length: 2,
      initialIndex: widget.initialTab,
      child: Builder(
        builder: (ctx) {
          final tabController = DefaultTabController.of(ctx);
          return Scaffold(
            backgroundColor: Colors.grey[50],
            appBar: AppBar(
              title: AnimatedBuilder(
                animation: tabController.animation ?? tabController,
                builder: (_, __) {
                  final idx = tabController.index;
                  return Text(
                      'Fatura / Gider - ${idx == 0 ? 'Fatura' : 'Gider'}');
                },
              ),
              actions: [
                IconButton(
                  onPressed: () {
                    Navigator.of(context).push(
                      MaterialPageRoute(
                          builder: (_) => MainMenuPage(
                              api: widget.api, onLogout: widget.onLogout)),
                    );
                  },
                  icon: const Icon(Icons.home_outlined),
                  tooltip: 'Ana Ekran',
                ),
                IconButton(
                  onPressed: () async {
                    await widget.onLogout();
                    if (!context.mounted) return;
                    Navigator.of(context).pop();
                  },
                  icon: const Icon(Icons.logout),
                  tooltip: 'Cikis',
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
    final priceLabel = _goldPrice == null
        ? 'Has altin fiyati girilmedi'
        : '${_fmtAmount(_goldPrice!.price, fractionDigits: 3)} TL/gr';
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: _loadingPricing
            ? const Row(children: [
                CircularProgressIndicator(),
                SizedBox(width: 12),
                Text('Fiyat yukleniyor...')
              ])
            : (_editingGold
                ? Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      const Text('Has Altin'),
                      const SizedBox(height: 8),
                      TextField(
                        controller: _goldController,
                        keyboardType: const TextInputType.numberWithOptions(
                            decimal: true),
                        inputFormatters: [
                          FilteringTextInputFormatter.allow(RegExp(r'[0-9.,]'))
                        ],
                        decoration: const InputDecoration(
                          hintText: 'Has altin fiyati',
                          suffixText: 'TL/gr',
                        ),
                      ),
                      const SizedBox(height: 12),
                      Row(
                        children: [
                          FilledButton(
                            onPressed: _savingGold ? null : _saveGoldPrice,
                            child: _savingGold
                                ? const SizedBox(
                                    height: 16,
                                    width: 16,
                                    child: CircularProgressIndicator(
                                        strokeWidth: 2),
                                  )
                                : const Text('Kaydet'),
                          ),
                          const SizedBox(width: 8),
                          TextButton(
                            onPressed: _savingGold
                                ? null
                                : () => setState(() => _editingGold = false),
                            child: const Text('Vazgec'),
                          ),
                        ],
                      ),
                    ],
                  )
                : InkWell(
                    onTap: () {
                      setState(() {
                        _editingGold = true;
                        _goldController.text =
                            _goldPrice?.price.toStringAsFixed(3) ?? '';
                      });
                    },
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        const Text('Has Altin'),
                        const SizedBox(height: 6),
                        Text(
                          priceLabel,
                          style: Theme.of(context).textTheme.headlineSmall,
                        ),
                      ],
                    ),
                  )),
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
      shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(8),
          side: BorderSide(color: Colors.amber.shade200)),
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
    try {
      return NumberFormat.decimalPattern('tr_TR').format(v);
    } catch (_) {
      return v.toStringAsFixed(2);
    }
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
              onChanged: (v) => _scheduleInvoiceSuggest(SuggestSource.name, v),
            ),
            SwitchListTile(
              value: invIsCompany,
              contentPadding: EdgeInsets.zero,
              title: const Text('VKN'),
              onChanged: (v) {
                setState(() {
                  invIsCompany = v;
                  if (!v) {
                    invVkn.clear();
                    invCompanyName.clear();
                    invIsForCompany = false;
                    invCompanyChoiceTouched = false;
                  }
                });
              },
            ),
            if (invIsCompany) ...[
              TextField(
                controller: invCompanyName,
                decoration: const InputDecoration(labelText: 'Şirket Adı'),
                textCapitalization: TextCapitalization.characters,
                inputFormatters: const [TurkishUppercaseTextFormatter()],
              ),
              const SizedBox(height: 8),
            ],
            const SizedBox(height: 8),
            TextField(
              controller: invTckn,
              decoration: const InputDecoration(labelText: 'TCKN (11 hane)'),
              keyboardType: TextInputType.number,
              inputFormatters: [
                FilteringTextInputFormatter.digitsOnly,
                LengthLimitingTextInputFormatter(11),
              ],
              onChanged: (v) => _scheduleInvoiceSuggest(SuggestSource.tckn, v),
            ),
            if (invIsCompany) ...[
              const SizedBox(height: 8),
              TextField(
                controller: invVkn,
                decoration: const InputDecoration(labelText: 'VKN (10 hane)'),
                keyboardType: TextInputType.number,
                inputFormatters: [
                  FilteringTextInputFormatter.digitsOnly,
                  LengthLimitingTextInputFormatter(10),
                ],
                onChanged: (v) {
                  _scheduleInvoiceSuggest(SuggestSource.vkn, v);
                  final hasVkn = v.trim().isNotEmpty;
                  if (!hasVkn) {
                    setState(() {
                      invIsForCompany = false;
                      invCompanyChoiceTouched = false;
                    });
                  } else if (!invCompanyChoiceTouched && !invIsForCompany) {
                    setState(() => invIsForCompany = true);
                  }
                },
              ),
            ],
            _suggestionList(_invSuggestions, _applyInvoiceSuggestion),
            const SizedBox(height: 8),
            if (_invNeedsContact)
              const Text(
                'Daha önce gelen müşteri için telefon veya e-posta alınmamış. Lütfen ekleyin (zorunlu değil).',
                style: TextStyle(fontSize: 12, color: Colors.black54),
              ),
            if (_invNeedsContact) const SizedBox(height: 8),
            TextField(
              controller: invPhone,
              decoration: const InputDecoration(labelText: 'Telefon (isteğe bağlı)'),
              keyboardType: TextInputType.phone,
              inputFormatters: [
                FilteringTextInputFormatter.allow(RegExp(r'[0-9+]')),
                LengthLimitingTextInputFormatter(20),
              ],
              onChanged: (v) {
                if (_invNeedsContact && v.trim().isNotEmpty) {
                  setState(() => _invNeedsContact = false);
                }
              },
            ),
            const SizedBox(height: 8),
            TextField(
              controller: invEmail,
              decoration: const InputDecoration(labelText: 'Email (isteğe bağlı)'),
              keyboardType: TextInputType.emailAddress,
              onChanged: (v) {
                if (_invNeedsContact && v.trim().isNotEmpty) {
                  setState(() => _invNeedsContact = false);
                }
              },
            ),
            const SizedBox(height: 8),
            TextField(
              controller: invTutar,
              decoration: const InputDecoration(labelText: 'Tutar (TL)')
                  .copyWith(prefixText: '₺ '),
              keyboardType: TextInputType.number,
              inputFormatters: [
                FilteringTextInputFormatter.digitsOnly,
                const ThousandsSeparatorInputFormatter(),
              ],
            ),
            const SizedBox(height: 12),
            _ayarToggle(
                current: invAyar,
                onChanged: (v) => setState(() => invAyar = v)),
            if (invVkn.text.trim().isNotEmpty) ...[
              const SizedBox(height: 8),
              Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Text('Kesim Tipi'),
                  const SizedBox(height: 4),
                  Center(
                    child: ToggleButtons(
                      isSelected: [invIsForCompany, !invIsForCompany],
                      onPressed: (i) {
                        setState(() {
                          invIsForCompany = i == 0;
                          invCompanyChoiceTouched = true;
                        });
                      },
                      children: const [
                        Padding(
                            padding: EdgeInsets.symmetric(horizontal: 16),
                            child: Text('Şirket')),
                        Padding(
                            padding: EdgeInsets.symmetric(horizontal: 16),
                            child: Text('Bireysel')),
                      ],
                    ),
                  ),
                ],
              ),
            ],
            const SizedBox(height: 12),
            _odemeToggle(
                current: invOdeme,
                onChanged: (v) => setState(() => invOdeme = v)),
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
              onChanged: (v) => _scheduleExpenseSuggest(SuggestSource.name, v),
            ),
            SwitchListTile(
              value: expIsCompany,
              contentPadding: EdgeInsets.zero,
              title: const Text('VKN'),
              onChanged: (v) {
                setState(() {
                  expIsCompany = v;
                  if (!v) {
                    expVkn.clear();
                    expCompanyName.clear();
                    expIsForCompany = false;
                    expCompanyChoiceTouched = false;
                  }
                });
              },
            ),
            if (expIsCompany) ...[
              TextField(
                controller: expCompanyName,
                decoration: const InputDecoration(labelText: 'Şirket Adı'),
                textCapitalization: TextCapitalization.characters,
                inputFormatters: const [TurkishUppercaseTextFormatter()],
              ),
              const SizedBox(height: 8),
            ],
            const SizedBox(height: 8),
            TextField(
              controller: expTckn,
              decoration: const InputDecoration(labelText: 'TCKN (11 hane)'),
              keyboardType: TextInputType.number,
              inputFormatters: [
                FilteringTextInputFormatter.digitsOnly,
                LengthLimitingTextInputFormatter(11),
              ],
              onChanged: (v) => _scheduleExpenseSuggest(SuggestSource.tckn, v),
            ),
            if (expIsCompany) ...[
              const SizedBox(height: 8),
              TextField(
                controller: expVkn,
                decoration: const InputDecoration(labelText: 'VKN (10 hane)'),
                keyboardType: TextInputType.number,
                inputFormatters: [
                  FilteringTextInputFormatter.digitsOnly,
                  LengthLimitingTextInputFormatter(10),
                ],
                onChanged: (v) {
                  _scheduleExpenseSuggest(SuggestSource.vkn, v);
                  final hasVkn = v.trim().isNotEmpty;
                  if (!hasVkn) {
                    setState(() {
                      expIsForCompany = false;
                      expCompanyChoiceTouched = false;
                    });
                  } else if (!expCompanyChoiceTouched && !expIsForCompany) {
                    setState(() => expIsForCompany = true);
                  }
                },
              ),
            ],
            _suggestionList(_expSuggestions, _applyExpenseSuggestion),
            const SizedBox(height: 8),
            if (_expNeedsContact)
              const Text(
                'Daha önce gelen müşteri için telefon veya e-posta alınmamış. Lütfen ekleyin (zorunlu değil).',
                style: TextStyle(fontSize: 12, color: Colors.black54),
              ),
            if (_expNeedsContact) const SizedBox(height: 8),
            TextField(
              controller: expPhone,
              decoration:
                  const InputDecoration(labelText: 'Telefon (isteğe bağlı)'),
              keyboardType: TextInputType.phone,
              inputFormatters: [
                FilteringTextInputFormatter.allow(RegExp(r'[0-9+]')),
                LengthLimitingTextInputFormatter(20),
              ],
              onChanged: (v) {
                if (_expNeedsContact && v.trim().isNotEmpty) {
                  setState(() => _expNeedsContact = false);
                }
              },
            ),
            const SizedBox(height: 8),
            TextField(
              controller: expEmail,
              decoration: const InputDecoration(labelText: 'Email (isteğe bağlı)'),
              keyboardType: TextInputType.emailAddress,
              onChanged: (v) {
                if (_expNeedsContact && v.trim().isNotEmpty) {
                  setState(() => _expNeedsContact = false);
                }
              },
            ),
            const SizedBox(height: 8),
            TextField(
              controller: expTutar,
              decoration: const InputDecoration(labelText: 'Tutar (TL)')
                  .copyWith(prefixText: '₺ '),
              keyboardType: TextInputType.number,
              inputFormatters: [
                FilteringTextInputFormatter.digitsOnly,
                const ThousandsSeparatorInputFormatter(),
              ],
            ),
            const SizedBox(height: 12),
            _ayarToggle(
                current: expAyar,
                onChanged: (v) => setState(() => expAyar = v)),
            if (expVkn.text.trim().isNotEmpty) ...[
              const SizedBox(height: 8),
              Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Text('Kesim Tipi'),
                  const SizedBox(height: 4),
                  Center(
                    child: ToggleButtons(
                      isSelected: [expIsForCompany, !expIsForCompany],
                      onPressed: (i) {
                        setState(() {
                          expIsForCompany = i == 0;
                          expCompanyChoiceTouched = true;
                        });
                      },
                      children: const [
                        Padding(
                            padding: EdgeInsets.symmetric(horizontal: 16),
                            child: Text('Şirket')),
                        Padding(
                            padding: EdgeInsets.symmetric(horizontal: 16),
                            child: Text('Bireysel')),
                      ],
                    ),
                  ),
                ],
              ),
            ],
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
            onPressed: (i) =>
                onChanged(i == 0 ? AltinAyar.Ayar22 : AltinAyar.Ayar24),
            children: const [
              Padding(
                  padding: EdgeInsets.symmetric(horizontal: 16),
                  child: Text('22 Ayar')),
              Padding(
                  padding: EdgeInsets.symmetric(horizontal: 16),
                  child: Text('24 Ayar')),
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
            onPressed: (i) =>
                onChanged(i == 0 ? OdemeSekli.Havale : OdemeSekli.KrediKarti),
            children: const [
              Padding(
                  padding: EdgeInsets.symmetric(horizontal: 16),
                  child: Text('Havale')),
              Padding(
                  padding: EdgeInsets.symmetric(horizontal: 16),
                  child: Text('Kredi Kartı')),
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
    final companyName = invCompanyName.text.trim();
    final vkn = invVkn.text.trim();
    final tutar = _parseAmount(invTutar.text);
    if (name.isEmpty || tckn.isEmpty || tutar == null) {
      _showError('Lütfen tüm alanları doldurun');
      return;
    }
    if (!_validateTckn(tckn)) {
      _showError('TCKN geçersiz!');
      return;
    }
    if (invIsCompany) {
      if (companyName.isEmpty || vkn.isEmpty) {
        _showError('Şirket adı ve VKN gerekli');
        return;
      }
      if (!_validateVkn(vkn)) {
        _showError('VKN geçersiz');
        return;
      }
    }
    try {
      setState(() {
        creatingInv = true;
        invResult = null;
      });
      final dto = CreateInvoiceDto(
        tarih: invDate,
        musteriAdSoyad: name,
        tckn: tckn,
        isCompany: invIsCompany,
        vknNo: invIsCompany ? vkn : null,
        companyName: invIsCompany ? companyName : null,
        isForCompany: invIsForCompany,
        tutar: tutar,
        odemeSekli: invOdeme,
        altinAyar: invAyar,
        telefon: invPhone.text.trim().isEmpty ? null : invPhone.text.trim(),
        email: invEmail.text.trim().isEmpty ? null : invEmail.text.trim(),
      );
      final created = await InvoiceService(widget.api).createDraft(dto);
      setState(() {
        invDraftId = created.id;
        invPredictedSira = created.siraNo;
        invAltinSatis = created.altinSatisFiyati;
      });
      await _showInvoicePreviewDialog();
    } catch (e) {
      _showError('Önizleme oluşturulamadı!');
    } finally {
      if (mounted) {
        setState(() {
          creatingInv = false;
        });
      }
    }
  }

  Future<void> _previewExpense() async {
    final name = expName.text.trim();
    final tckn = expTckn.text.trim();
    final companyName = expCompanyName.text.trim();
    final vkn = expVkn.text.trim();
    final tutar = _parseAmount(expTutar.text);
    if (name.isEmpty || tckn.isEmpty || tutar == null) {
      _showError('Lütfen tüm alanları doldurun');
      return;
    }
    if (!_validateTckn(tckn)) {
      _showError('TCKN geçersiz');
      return;
    }
    if (expIsCompany) {
      if (companyName.isEmpty || vkn.isEmpty) {
        _showError('Şirket adı ve VKN gerekli');
        return;
      }
      if (!_validateVkn(vkn)) {
        _showError('VKN geçersiz');
        return;
      }
    }
    try {
      setState(() {
        creatingExp = true;
        expResult = null;
      });
      final dto = CreateExpenseDto(
        tarih: expDate,
        musteriAdSoyad: name,
        tckn: tckn,
        isCompany: expIsCompany,
        vknNo: expIsCompany ? vkn : null,
        companyName: expIsCompany ? companyName : null,
        isForCompany: expIsForCompany,
        tutar: tutar,
        altinAyar: expAyar,
        telefon: expPhone.text.trim().isEmpty ? null : expPhone.text.trim(),
        email: expEmail.text.trim().isEmpty ? null : expEmail.text.trim(),
      );
      final created = await ExpenseService(widget.api).createDraft(dto);
      setState(() {
        expDraftId = created.id;
        expPredictedSira = created.siraNo;
        expAltinSatis = created.altinSatisFiyati;
      });
      await _showExpensePreviewDialog();
    } catch (e) {
      _showError('Önizleme oluşturulamadı!');
    } finally {
      if (mounted) {
        setState(() {
          creatingExp = false;
        });
      }
    }
  }

  void _resetInvoiceForm({bool keepResult = false}) {
    setState(() {
      invName.clear();
      invTckn.clear();
      invVkn.clear();
      invCompanyName.clear();
      invIsCompany = false;
      invIsForCompany = false;
      invCompanyChoiceTouched = false;
      invTutar.clear();
      invPhone.clear();
      invEmail.clear();
      invDate = DateTime.now();
      invOdeme = OdemeSekli.Havale;
      invAyar = AltinAyar.Ayar22;
      _invSuggestions = [];
      _invNeedsContact = false;
      if (!keepResult) invResult = null;
      invDraftId = null;
      invPredictedSira = null;
      invAltinSatis = null;
    });
  }

  void _resetExpenseForm({bool keepResult = false}) {
    setState(() {
      expName.clear();
      expTckn.clear();
      expVkn.clear();
      expCompanyName.clear();
      expIsCompany = false;
      expIsForCompany = false;
      expCompanyChoiceTouched = false;
      expTutar.clear();
      expPhone.clear();
      expEmail.clear();
      expDate = DateTime.now();
      expAyar = AltinAyar.Ayar22;
      _expSuggestions = [];
      _expNeedsContact = false;
      if (!keepResult) expResult = null;
      expDraftId = null;
      expPredictedSira = null;
      expAltinSatis = null;
    });
  }

  bool _validateTckn(String id) {
    if (!RegExp(r'^\d{11}$').hasMatch(id)) return false;
    if (id.startsWith('0')) return false;
    final digits =
        id.split('').take(11).map((e) => int.tryParse(e) ?? 0).toList();
    final calc10 =
        (((digits[0] + digits[2] + digits[4] + digits[6] + digits[8]) * 7) -
                (digits[1] + digits[3] + digits[5] + digits[7])) %
            10;
    final calc11 = (digits.take(10).reduce((a, b) => a + b)) % 10;
    return digits[9] == calc10 && digits[10] == calc11;
  }

  bool _validateVkn(String vkn) {
    if (!RegExp(r'^\d{10}$').hasMatch(vkn)) return false;
    final digits =
        vkn.split('').take(10).map((e) => int.tryParse(e) ?? 0).toList();
    var sum = 0;
    for (var i = 0; i < 9; i++) {
      final digit = digits[i];
      final tmp = (digit + 10 - (i + 1)) % 10;
      final pow = (Math.pow(2, 9 - i) % 9).toInt();
      var res = (tmp * pow) % 9;
      if (tmp != 0 && res == 0) res = 9;
      sum += res;
    }
    final checkDigit = (10 - (sum % 10)) % 10;
    return digits[9] == checkDigit;
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
            if (invAltinSatis != null)
              Text('Altın Satış Fiyatı: ${_fmtAmount(invAltinSatis!)} TL/gr'),
            const SizedBox(height: 8),
            Text('Tarih: ${_formatDate(invDate)}'),
            Text('Ad Soyad: $name'),
            Text('TCKN: $tckn'),
            if (invIsCompany)
              Text('Şirket Adı: ${invCompanyName.text.trim()}'),
            if (invIsCompany) Text('VKN: ${invVkn.text.trim()}'),
            Text('Tutar: ${_fmtAmount(tutar)} TL'),
            Text(
                'Ayar: ${invAyar == AltinAyar.Ayar22 ? '22 Ayar' : '24 Ayar'}'),
            _buildDerivedPreview(tutar, invAltinSatis, invAyar),
            Text(
                'Ödeme: ${invOdeme == OdemeSekli.Havale ? 'Havale' : 'Kredi Kartı'}'),
          ],
        ),
        actions: [
          TextButton(
              onPressed: () async {
                final id = invDraftId; // cancel preview and delete draft
                Navigator.of(context).pop();
                if (id != null) {
                  try {
                    await InvoiceService(widget.api).deleteIfNotKesildi(id);
                  } catch (_) {}
                }
                setState(() {
                  invDraftId = null;
                  invPredictedSira = null;
                  invAltinSatis = null;
                });
              },
              child: const Text('İptal')),
          FilledButton(
              onPressed: () async {
                final id = invDraftId;
                if (id == null) return;
                try {
                  await InvoiceService(widget.api).finalize(id);
                  if (!mounted) return;
                  setState(() {
                    invResult = 'Kesildi: Sıra No $invPredictedSira';
                  });
                  Navigator.of(context).pop();
                  if (!mounted) return;
                  _resetInvoiceForm(keepResult: true);
                } catch (e) {
                  _showError('Fatura kesilemedi');
                }
              },
              child: const Text('Kes')),
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
            if (expAltinSatis != null)
              Text('Altın Satış: ${_fmtAmount(expAltinSatis!)} TL/gr'),
            const SizedBox(height: 8),
            Text('Tarih: ${_formatDate(expDate)}'),
            Text('Ad Soyad: $name'),
            Text('TCKN: $tckn'),
            if (expIsCompany)
              Text('Şirket Adı: ${expCompanyName.text.trim()}'),
            if (expIsCompany) Text('VKN: ${expVkn.text.trim()}'),
            Text('Tutar: ${_fmtAmount(tutar)} TL'),
            Text(
                'Ayar: ${expAyar == AltinAyar.Ayar22 ? '22 Ayar' : '24 Ayar'}'),
            _buildDerivedPreview(tutar, expAltinSatis, expAyar),
          ],
        ),
        actions: [
          TextButton(
              onPressed: () async {
                final id = expDraftId;
                Navigator.of(context).pop();
                if (id != null) {
                  try {
                    await ExpenseService(widget.api).deleteIfNotKesildi(id);
                  } catch (_) {}
                }
                setState(() {
                  expDraftId = null;
                  expPredictedSira = null;
                  expAltinSatis = null;
                });
              },
              child: const Text('İptal')),
          FilledButton(
              onPressed: () async {
                final id = expDraftId;
                if (id == null) return;
                try {
                  await ExpenseService(widget.api).finalize(id);
                  if (!mounted) return;
                  setState(() {
                    expResult = 'Kesildi: Sıra No $expPredictedSira';
                  });
                  Navigator.of(context).pop();
                  if (!mounted) return;
                  _resetExpenseForm(keepResult: true);
                } catch (e) {
                  _showError('Gider kesilemedi');
                }
              },
              child: const Text('Kes')),
        ],
      ),
    );
  }

  void _showError(String msg) {
    ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(msg)));
  }

  String _formatApiError(Object err, String fallback) {
    if (err is ApiError) {
      final msg = err.body['error'] ?? err.body['message'];
      if (msg is String && msg.trim().isNotEmpty) return msg;
      return '$fallback (HTTP ${err.status})';
    }
    return fallback;
  }

  String _fmtAmount(num v, {int fractionDigits = 0}) {
    final f = NumberFormat.decimalPattern('tr_TR');
    f.minimumFractionDigits = fractionDigits;
    f.maximumFractionDigits = fractionDigits;
    return f.format(v);
  }

  double? _parseDecimal(String s) {
    final cleaned = s.replaceAll(' ', '').replaceAll(',', '.');
    return double.tryParse(cleaned);
  }

  double? _parseAmount(String s) {
    final cleaned = s.replaceAll('.', '').replaceAll(',', '.');
    return double.tryParse(cleaned);
  }

  // Compute and render derived preview metrics (saf altın, yeni ürün, gram, işçilik)
  Widget _buildDerivedPreview(
      double tutar, double? altinSatis, AltinAyar ayar) {
    if (altinSatis == null) return const SizedBox.shrink();
    double r2(double x) => (x * 100).round() / 100.0;
    final safAltin =
        r2(ayar == AltinAyar.Ayar22 ? altinSatis * 0.916 : altinSatis * 0.995);
    final yeniUrun =
        r2(ayar == AltinAyar.Ayar22 ? tutar * 0.99 : tutar * 0.998);
    final gram = safAltin == 0 ? 0.0 : r2(yeniUrun / safAltin);
    final altinHizmet = r2(gram * safAltin);
    final iscilikKdvli = r2(r2(tutar) - altinHizmet);
    final iscilik = r2(iscilikKdvli / 1.20);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const SizedBox(height: 8),
        Text(
            'Saf Altın Değeri: ${_fmtAmount(safAltin, fractionDigits: 2)} TL/gr'),
        Text('Yeni Ürün Fiyatı: ${_fmtAmount(yeniUrun, fractionDigits: 2)} TL'),
        Text('Gram Değeri: ${_fmtAmount(gram, fractionDigits: 2)} gr'),
        Text(
            'İşçilik (KDV\'siz): ${_fmtAmount(iscilik, fractionDigits: 2)} TL'),
      ],
    );
  }
}

class ThousandsSeparatorInputFormatter extends TextInputFormatter {
  const ThousandsSeparatorInputFormatter();

  @override
  TextEditingValue formatEditUpdate(
      TextEditingValue oldValue, TextEditingValue newValue) {
    final rawDigits = newValue.text.replaceAll(RegExp(r'[^0-9]'), '');
    if (rawDigits.isEmpty) {
      return const TextEditingValue(
          text: '', selection: TextSelection.collapsed(offset: 0));
    }
    final number = int.parse(rawDigits);
    final formatted = NumberFormat.decimalPattern('tr_TR')
      ..minimumFractionDigits = 0
      ..maximumFractionDigits = 0;
    final newText = formatted.format(number);

    final cursorFromRight =
        newValue.text.length - newValue.selection.extentOffset;
    var newOffset = newText.length - cursorFromRight;
    if (newOffset < 0) newOffset = 0;
    if (newOffset > newText.length) newOffset = newText.length;
    return TextEditingValue(
        text: newText, selection: TextSelection.collapsed(offset: newOffset));
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
  TextEditingValue formatEditUpdate(
      TextEditingValue oldValue, TextEditingValue newValue) {
    final up = _trUpper(newValue.text);
    final sel = newValue.selection;
    final offset = sel.isValid ? sel.baseOffset : up.length;
    final safeOffset = offset.clamp(0, up.length);
    return TextEditingValue(
        text: up, selection: TextSelection.collapsed(offset: safeOffset));
  }
}
