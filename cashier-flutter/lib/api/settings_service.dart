import 'api_client.dart';

class CalcSettings {
  final bool defaultKariHesapla;
  final double karMargin;
  final int decimalPrecision;
  final String karMilyemFormulaType; // basic | withMargin | custom
  final bool showPercentage;
  final bool includeTax;
  final double taxRate;

  const CalcSettings({
    required this.defaultKariHesapla,
    required this.karMargin,
    required this.decimalPrecision,
    required this.karMilyemFormulaType,
    required this.showPercentage,
    required this.includeTax,
    required this.taxRate,
  });

  static const defaults = CalcSettings(
    defaultKariHesapla: true,
    karMargin: 0,
    decimalPrecision: 2,
    karMilyemFormulaType: 'basic',
    showPercentage: true,
    includeTax: false,
    taxRate: 0,
  );
}

class SettingsService {
  final ApiClient _api;
  SettingsService(this._api);

  Future<double> getMilyem() async {
    try {
      final json = await _api.getJson('/api/settings/milyem');
      final v = json['value'];
      if (v is num) return v.toDouble();
      return double.tryParse('$v') ?? 1000.0;
    } catch (_) {
      return 1000.0; // default (no change)
    }
  }

  Future<CalcSettings> getCalcSettings() async {
    try {
      final j = await _api.getJson('/api/settings/calc');
      return CalcSettings(
        defaultKariHesapla: (j['defaultKariHesapla'] ?? true) == true,
        karMargin: (j['karMargin'] is num) ? (j['karMargin'] as num).toDouble() : double.tryParse('${j['karMargin']}') ?? 0.0,
        decimalPrecision: (j['decimalPrecision'] is num) ? (j['decimalPrecision'] as num).toInt() : int.tryParse('${j['decimalPrecision']}') ?? 2,
        karMilyemFormulaType: (j['karMilyemFormulaType']?.toString() ?? 'basic'),
        showPercentage: (j['showPercentage'] ?? true) == true,
        includeTax: (j['includeTax'] ?? false) == true,
        taxRate: (j['taxRate'] is num) ? (j['taxRate'] as num).toDouble() : double.tryParse('${j['taxRate']}') ?? 0.0,
      );
    } catch (_) {
      return CalcSettings.defaults;
    }
  }
}
