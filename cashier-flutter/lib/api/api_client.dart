import 'dart:convert';
import 'package:http/http.dart' as http;
import 'package:shared_preferences/shared_preferences.dart';

class ApiClient {
  // Use --dart-define=API_BASE=https://host:port for overrides
  static const _defaultBase = String.fromEnvironment('API_BASE', defaultValue: 'http://161.97.97.216:9002');
  final String baseUrl;
  String? _token;

  ApiClient({String? baseUrl}) : baseUrl = (baseUrl ?? _defaultBase).replaceAll(RegExp(r'/+$'), '');

  Future<void> loadToken() async {
    final sp = await SharedPreferences.getInstance();
    _token = sp.getString('auth_token');
  }

  bool get hasToken => _token != null && _token!.isNotEmpty;

  Future<void> saveToken(String token) async {
    _token = token;
    final sp = await SharedPreferences.getInstance();
    await sp.setString('auth_token', token);
  }

  Future<void> clearToken() async {
    _token = null;
    final sp = await SharedPreferences.getInstance();
    await sp.remove('auth_token');
  }

  Map<String, String> _headers({Map<String, String>? extra}) {
    final h = <String, String>{
      'Content-Type': 'application/json; charset=utf-8',
    };
    if (_token != null && _token!.isNotEmpty) {
      h['Authorization'] = 'Bearer $_token';
    }
    if (extra != null) h.addAll(extra);
    return h;
  }

  Uri _uri(String path, [Map<String, dynamic>? query]) {
    final cleaned = path.startsWith('/') ? path : '/$path';
    return Uri.parse('$baseUrl$cleaned').replace(queryParameters: query?.map((k, v) => MapEntry(k, '$v')));
  }

  Future<Map<String, dynamic>> postJson(String path, Map<String, dynamic> body, {Map<String, dynamic>? query}) async {
    final resp = await http.post(_uri(path, query), headers: _headers(), body: jsonEncode(body));
    if (resp.statusCode >= 200 && resp.statusCode < 300) {
      return resp.body.isEmpty ? <String, dynamic>{} : jsonDecode(utf8.decode(resp.bodyBytes)) as Map<String, dynamic>;
    }
    throw ApiError(resp.statusCode, _safeDecode(resp.bodyBytes));
  }

  Future<Map<String, dynamic>> getJson(String path, {Map<String, dynamic>? query}) async {
    final resp = await http.get(_uri(path, query), headers: _headers());
    if (resp.statusCode >= 200 && resp.statusCode < 300) {
      return resp.body.isEmpty ? <String, dynamic>{} : jsonDecode(utf8.decode(resp.bodyBytes)) as Map<String, dynamic>;
    }
    throw ApiError(resp.statusCode, _safeDecode(resp.bodyBytes));
  }

  Future<void> putJson(String path, Map<String, dynamic> body) async {
    final resp = await http.put(_uri(path), headers: _headers(), body: jsonEncode(body));
    if (resp.statusCode >= 200 && resp.statusCode < 300) return;
    throw ApiError(resp.statusCode, _safeDecode(resp.bodyBytes));
  }

  Future<void> delete(String path) async {
    final resp = await http.delete(_uri(path), headers: _headers());
    if (resp.statusCode >= 200 && resp.statusCode < 300) return;
    throw ApiError(resp.statusCode, _safeDecode(resp.bodyBytes));
  }

  static Map<String, dynamic> _safeDecode(List<int> bytes) {
    try {
      return jsonDecode(utf8.decode(bytes)) as Map<String, dynamic>;
    } catch (_) {
      return {'error': 'Unexpected error'};
    }
  }
}

class ApiError implements Exception {
  final int status;
  final Map<String, dynamic> body;
  ApiError(this.status, this.body);
  @override
  String toString() => 'ApiError($status, $body)';
}
