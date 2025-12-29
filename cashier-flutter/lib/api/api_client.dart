import 'dart:convert';
import 'package:http/http.dart' as http;
import 'package:shared_preferences/shared_preferences.dart';

class ApiClient {
  // Use --dart-define=API_BASE=https://host:port for overrides
  static const _defaultBase = String.fromEnvironment('API_BASE', defaultValue: 'http://localhost:8080');
  static const _logHttp = bool.fromEnvironment('LOG_HTTP', defaultValue: false);
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

  void _logRequest(String method, Uri uri, Map<String, String> headers, [Object? body]) {
    if (!_logHttp) return;
    final safeHeaders = Map<String, String>.from(headers);
    if (safeHeaders.containsKey('Authorization')) {
      safeHeaders['Authorization'] = 'Bearer ***';
    }
    print('HTTP $method $uri');
    print('Headers: $safeHeaders');
    if (body != null) {
      print('Body: $body');
    }
  }

  void _logResponse(String method, Uri uri, http.Response resp) {
    if (!_logHttp) return;
    final text = utf8.decode(resp.bodyBytes);
    print('HTTP $method $uri -> ${resp.statusCode}');
    if (text.isNotEmpty) {
      print('Response: $text');
    }
  }

  Future<Map<String, dynamic>> postJson(String path, Map<String, dynamic> body, {Map<String, dynamic>? query}) async {
    final uri = _uri(path, query);
    final headers = _headers();
    final payload = jsonEncode(body);
    _logRequest('POST', uri, headers, payload);
    final resp = await http.post(uri, headers: headers, body: payload);
    _logResponse('POST', uri, resp);
    if (resp.statusCode >= 200 && resp.statusCode < 300) {
      return resp.body.isEmpty ? <String, dynamic>{} : jsonDecode(utf8.decode(resp.bodyBytes)) as Map<String, dynamic>;
    }
    throw ApiError(resp.statusCode, _safeDecode(resp.bodyBytes));
  }

  Future<Map<String, dynamic>> getJson(String path, {Map<String, dynamic>? query}) async {
    final uri = _uri(path, query);
    final headers = _headers();
    _logRequest('GET', uri, headers);
    final resp = await http.get(uri, headers: headers);
    _logResponse('GET', uri, resp);
    if (resp.statusCode >= 200 && resp.statusCode < 300) {
      return resp.body.isEmpty ? <String, dynamic>{} : jsonDecode(utf8.decode(resp.bodyBytes)) as Map<String, dynamic>;
    }
    throw ApiError(resp.statusCode, _safeDecode(resp.bodyBytes));
  }

  Future<dynamic> getJsonAny(String path, {Map<String, dynamic>? query}) async {
    final uri = _uri(path, query);
    final headers = _headers();
    _logRequest('GET', uri, headers);
    final resp = await http.get(uri, headers: headers);
    _logResponse('GET', uri, resp);
    if (resp.statusCode >= 200 && resp.statusCode < 300) {
      if (resp.body.isEmpty) return null;
      return jsonDecode(utf8.decode(resp.bodyBytes));
    }
    throw ApiError(resp.statusCode, _safeDecode(resp.bodyBytes));
  }

  Future<Map<String, dynamic>> putJson(String path, Map<String, dynamic> body) async {
    final uri = _uri(path);
    final headers = _headers();
    final payload = jsonEncode(body);
    _logRequest('PUT', uri, headers, payload);
    final resp = await http.put(uri, headers: headers, body: payload);
    _logResponse('PUT', uri, resp);
    if (resp.statusCode >= 200 && resp.statusCode < 300) {
      return resp.body.isEmpty ? <String, dynamic>{} : jsonDecode(utf8.decode(resp.bodyBytes)) as Map<String, dynamic>;
    }
    throw ApiError(resp.statusCode, _safeDecode(resp.bodyBytes));
  }

  Future<void> delete(String path) async {
    final uri = _uri(path);
    final headers = _headers();
    _logRequest('DELETE', uri, headers);
    final resp = await http.delete(uri, headers: headers);
    _logResponse('DELETE', uri, resp);
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
