import 'package:flutter/material.dart';
import 'api/api_client.dart';
import 'pages/login_page.dart';
import 'pages/home_page.dart';

class AuthController extends ChangeNotifier {
  final ApiClient api;
  bool loading = true;
  bool authenticated = false;

  AuthController(this.api);

  Future<void> initialize() async {
    loading = true;
    notifyListeners();
    await api.loadToken();
    authenticated = api.hasToken;
    loading = false;
    notifyListeners();
  }

  void markLoggedIn() {
    authenticated = true;
    notifyListeners();
  }

  Future<void> logout() async {
    await api.clearToken();
    authenticated = false;
    notifyListeners();
  }
}

class CashierApp extends StatefulWidget {
  final ApiClient api;
  const CashierApp({super.key, required this.api});

  @override
  State<CashierApp> createState() => _CashierAppState();
}

class _CashierAppState extends State<CashierApp> {
  late final AuthController _auth;

  @override
  void initState() {
    super.initState();
    _auth = AuthController(widget.api);
    _auth.initialize();
  }

  @override
  void dispose() {
    widget.api.dispose();
    _auth.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return AnimatedBuilder(
      animation: _auth,
      builder: (context, _) {
        return MaterialApp(
          title: 'Kasiyer',
          theme: ThemeData(useMaterial3: true, colorSchemeSeed: Colors.amber),
          home: _auth.loading
              ? const Scaffold(body: Center(child: CircularProgressIndicator()))
              : _auth.authenticated
                  ? HomePage(api: widget.api, onLogout: _logout)
                  : LoginPage(api: widget.api, onLoggedIn: _onLoggedIn),
        );
      },
    );
  }

  void _onLoggedIn() {
    _auth.markLoggedIn();
  }

  Future<void> _logout() async {
    await _auth.logout();
    if (mounted) {
      Navigator.of(context).popUntil((route) => route.isFirst);
    }
  }
}
