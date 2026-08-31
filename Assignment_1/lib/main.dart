import 'package:flutter/material.dart';

void main() {
  runApp(const MeasuresConverterApp());
}

/// Root widget for the Measures Converter application.
class MeasuresConverterApp extends StatelessWidget {
  const MeasuresConverterApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'Measures Converter',
      debugShowCheckedModeBanner: false,
      theme: ThemeData(
        primarySwatch: Colors.blue,
        useMaterial3: true,
      ),
      home: const ConverterHomePage(),
    );
  }
}

/// A single unit of length, expressed as a conversion factor to meters.
///
/// Storing every unit relative to the same base (meters) means converting
/// between any two units only ever needs two multiplications, instead of
/// one formula per unit pair.
class LengthUnit {
  final String name;
  final double metersPerUnit;

  const LengthUnit(this.name, this.metersPerUnit);
}

/// Supported units of length, metric and imperial alike.
const List<LengthUnit> kLengthUnits = [
  LengthUnit('millimeters', 0.001),
  LengthUnit('centimeters', 0.01),
  LengthUnit('meters', 1.0),
  LengthUnit('kilometers', 1000.0),
  LengthUnit('inches', 0.0254),
  LengthUnit('feet', 0.3048),
  LengthUnit('yards', 0.9144),
  LengthUnit('miles', 1609.344),
];

class ConverterHomePage extends StatefulWidget {
  const ConverterHomePage({super.key});

  @override
  State<ConverterHomePage> createState() => _ConverterHomePageState();
}

class _ConverterHomePageState extends State<ConverterHomePage> {
  final TextEditingController _valueController = TextEditingController();

  String _fromUnitName = kLengthUnits[2].name; // meters
  String _toUnitName = kLengthUnits[5].name; // feet
  String _resultText = '';

  @override
  void dispose() {
    _valueController.dispose();
    super.dispose();
  }

  LengthUnit _unitByName(String name) =>
      kLengthUnits.firstWhere((unit) => unit.name == name);

  /// Converts the entered value from the selected "from" unit to the
  /// selected "to" unit and updates the displayed result.
  void _convert() {
    final double? inputValue = double.tryParse(_valueController.text);

    if (inputValue == null) {
      setState(() {
        _resultText = 'Please enter a valid number';
      });
      return;
    }

    final LengthUnit fromUnit = _unitByName(_fromUnitName);
    final LengthUnit toUnit = _unitByName(_toUnitName);

    // Convert to meters first, then from meters to the target unit.
    final double valueInMeters = inputValue * fromUnit.metersPerUnit;
    final double convertedValue = valueInMeters / toUnit.metersPerUnit;

    setState(() {
      _resultText =
          '$inputValue $_fromUnitName are ${convertedValue.toStringAsFixed(3)} $_toUnitName';
    });
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Measures Converter'),
        centerTitle: true,
      ),
      body: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 24.0, vertical: 16.0),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            const _SectionLabel('Value'),
            TextField(
              controller: _valueController,
              keyboardType: const TextInputType.numberWithOptions(
                decimal: true,
                signed: true,
              ),
              textAlign: TextAlign.center,
            ),
            const SizedBox(height: 24),
            const _SectionLabel('From'),
            _UnitDropdown(
              value: _fromUnitName,
              onChanged: (value) => setState(() => _fromUnitName = value),
            ),
            const SizedBox(height: 24),
            const _SectionLabel('To'),
            _UnitDropdown(
              value: _toUnitName,
              onChanged: (value) => setState(() => _toUnitName = value),
            ),
            const SizedBox(height: 24),
            Center(
              child: ElevatedButton(
                onPressed: _convert,
                child: const Text('Convert'),
              ),
            ),
            const SizedBox(height: 24),
            Text(
              _resultText,
              textAlign: TextAlign.center,
              style: const TextStyle(fontSize: 18),
            ),
          ],
        ),
      ),
    );
  }
}

/// Centered gray section heading used above each form field, matching the
/// app's "Value" / "From" / "To" labels.
class _SectionLabel extends StatelessWidget {
  final String text;

  const _SectionLabel(this.text);

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 8.0),
      child: Text(
        text,
        textAlign: TextAlign.center,
        style: TextStyle(fontSize: 18, color: Colors.grey[700]),
      ),
    );
  }
}

/// Dropdown listing every supported unit of length.
class _UnitDropdown extends StatelessWidget {
  final String value;
  final ValueChanged<String> onChanged;

  const _UnitDropdown({required this.value, required this.onChanged});

  @override
  Widget build(BuildContext context) {
    return DropdownButtonHideUnderline(
      child: InputDecorator(
        decoration: const InputDecoration(
          border: UnderlineInputBorder(),
        ),
        child: DropdownButton<String>(
          value: value,
          isExpanded: true,
          items: kLengthUnits
              .map((unit) => DropdownMenuItem(
                    value: unit.name,
                    child: Text(unit.name),
                  ))
              .toList(),
          onChanged: (newValue) {
            if (newValue != null) onChanged(newValue);
          },
        ),
      ),
    );
  }
}
