import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:measures_converter/main.dart';

void main() {
  testWidgets('Converts 100 meters to feet', (WidgetTester tester) async {
    await tester.pumpWidget(const MeasuresConverterApp());

    // Default units are meters -> feet; enter a value and tap Convert.
    await tester.enterText(find.byType(TextField), '100');
    await tester.tap(find.widgetWithText(ElevatedButton, 'Convert'));
    await tester.pump();

    expect(find.textContaining('328.084 feet'), findsOneWidget);
  });

  testWidgets('Shows an error for non-numeric input', (WidgetTester tester) async {
    await tester.pumpWidget(const MeasuresConverterApp());

    await tester.enterText(find.byType(TextField), 'abc');
    await tester.tap(find.widgetWithText(ElevatedButton, 'Convert'));
    await tester.pump();

    expect(find.text('Please enter a valid number'), findsOneWidget);
  });
}
