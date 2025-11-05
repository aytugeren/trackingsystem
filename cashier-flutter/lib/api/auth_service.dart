import 'api_client.dart';
import 'models.dart';

class AuthService {
  final ApiClient _api;
  AuthService(this._api);

  Future<LoginResponse> login(String email, String password) async {
    final json = await _api.postJson('/api/auth/login', {
      'email': email,
      'password': password,
    });
    final resp = LoginResponse.fromJson(json);
    await _api.saveToken(resp.token);
    return resp;
  }
}

