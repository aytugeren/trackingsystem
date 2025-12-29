import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'api/api_client.dart';
import 'pages/login_page.dart';
import 'pages/home_page.dart';

class CashierApp extends StatefulWidget {
  final ApiClient api;
  const CashierApp({super.key, required this.api});

  @override
  State<CashierApp> createState() => _CashierAppState();
}

class _CashierAppState extends State<CashierApp> {
  bool _loading = true;
  bool _authenticated = false;

  @override
  void initState() {
    super.initState();
    widget.api.loadToken().then((_) {
      setState(() {
        _authenticated = widget.api.hasToken; // only proceed if token exists
        _loading = false;
      });
    });
  }

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'Kasiyer',
      theme: ThemeData(useMaterial3: true, colorSchemeSeed: Colors.amber),
      locale: const Locale('tr', 'TR'),
      supportedLocales: const [
        Locale('tr', 'TR'),
        Locale('en', 'US'),
      ],
      localizationsDelegates: const [
        GlobalMaterialLocalizations.delegate,
        GlobalWidgetsLocalizations.delegate,
        GlobalCupertinoLocalizations.delegate,
      ],
      home: _loading
          ? const Scaffold(body: Center(child: CircularProgressIndicator()))
          : _authenticated
              ? HomePage(api: widget.api, onLogout: _logout)
              : LoginPage(api: widget.api, onLoggedIn: _onLoggedIn),
    );
  }

  void _onLoggedIn() {
    setState(() => _authenticated = true);
  }

  Future<void> _logout() async {
    await widget.api.clearToken();
    setState(() => _authenticated = false);
  }
}
