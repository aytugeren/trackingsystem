import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import 'package:intl/date_symbol_data_local.dart';
import 'app.dart';
import 'api/api_client.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  try {
    await initializeDateFormatting('tr_TR');
    Intl.defaultLocale = 'tr_TR';
  } catch (_) {
    // ignore; fallback to default locale
  }
  final api = ApiClient();
  runApp(CashierApp(api: api));
}
