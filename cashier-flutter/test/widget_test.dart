import 'package:cashier_flutter/api/api_client.dart';
import 'package:cashier_flutter/app.dart';
import 'package:flutter_test/flutter_test.dart';

class _FakeApiClient extends ApiClient {
  _FakeApiClient() : super(baseUrl: 'http://localhost');
  @override
  Future<void> loadToken() async {}
  @override
  bool get hasToken => false;
  @override
  Future<void> clearToken() async {}
}

void main() {
  testWidgets('Uygulama token yokken login ekranını gösterir', (tester) async {
    final api = _FakeApiClient();

    await tester.pumpWidget(CashierApp(api: api));
    await tester.pumpAndSettle();

    expect(find.text('Kasiyer Girisi'), findsOneWidget);
  });
}
