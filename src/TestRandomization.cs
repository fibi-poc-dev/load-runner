#!/usr/bin/env dotnet-script

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

Console.WriteLine("CSV Row Randomization Test");
Console.WriteLine("==========================");

try
{
    // Load CSV data directly
    var csvPath = "SampleData/referential_data.csv";
    if (!File.Exists(csvPath))
    {
        Console.WriteLine($"CSV file not found: {csvPath}");
        return 1;
    }
    
    var lines = File.ReadAllLines(csvPath);
    var headers = lines[0].Split(',');
    var dataRows = new List<Dictionary<string, string>>();
    
    // Parse CSV rows
    for (int i = 1; i < lines.Length; i++)
    {
        var values = lines[i].Split(',');
        var row = new Dictionary<string, string>();
        for (int j = 0; j < headers.Length && j < values.Length; j++)
        {
            row[headers[j]] = values[j];
        }
        dataRows.Add(row);
    }
    
    Console.WriteLine($"Loaded {dataRows.Count} rows from CSV file");
    
    // Test randomization by simulating virtual user row selection 200 times
    var selectedRows = new Dictionary<string, int>(); // BankId -> selection count
    var random = new Random();
    
    Console.WriteLine("\nSimulating 200 virtual user row selections:");
    for (int i = 0; i < 200; i++)
    {
        var dataRowIndex = random.Next(dataRows.Count);
        var dataRow = dataRows[dataRowIndex];
        var bankId = dataRow["BankId"];
        
        selectedRows[bankId] = selectedRows.GetValueOrDefault(bankId, 0) + 1;
        
        if (i < 20) // Show first 20 selections
        {
            Console.WriteLine($"  Selection {i+1:D2}: Row {dataRowIndex:D3} - BankId={bankId}");
        }
    }
    
    Console.WriteLine($"\n... (showing first 20 selections)");
    
    // Analyze distribution
    var uniqueRowsSelected = selectedRows.Keys.Count;
    var totalSelections = selectedRows.Values.Sum();
    var avgSelectionsPerRow = totalSelections / (double)uniqueRowsSelected;
    var maxSelections = selectedRows.Values.Max();
    var minSelections = selectedRows.Values.Min();
    
    Console.WriteLine($"\nRandomization Analysis:");
    Console.WriteLine($"  Total CSV rows available: {dataRows.Count}");
    Console.WriteLine($"  Unique rows selected: {uniqueRowsSelected}");
    Console.WriteLine($"  Total selections made: {totalSelections}");
    Console.WriteLine($"  Average selections per row: {avgSelectionsPerRow:F2}");
    Console.WriteLine($"  Max selections for any row: {maxSelections}");
    Console.WriteLine($"  Min selections for any row: {minSelections}");
    
    // Show distribution of most/least selected rows
    var topSelected = selectedRows.OrderByDescending(kvp => kvp.Value).Take(5);
    var leastSelected = selectedRows.OrderBy(kvp => kvp.Value).Take(5);
    
    Console.WriteLine($"\nTop 5 most selected rows:");
    foreach (var item in topSelected)
    {
        Console.WriteLine($"  BankId {item.Key}: selected {item.Value} times");
    }
    
    Console.WriteLine($"\nTop 5 least selected rows:");
    foreach (var item in leastSelected)
    {
        Console.WriteLine($"  BankId {item.Key}: selected {item.Value} times");
    }
    
    // Calculate distribution evenness (coefficient of variation)
    var stdDev = Math.Sqrt(selectedRows.Values.Select(v => Math.Pow(v - avgSelectionsPerRow, 2)).Sum() / uniqueRowsSelected);
    var coefficientOfVariation = stdDev / avgSelectionsPerRow;
    
    Console.WriteLine($"\nDistribution Analysis:");
    Console.WriteLine($"  Standard deviation: {stdDev:F2}");
    Console.WriteLine($"  Coefficient of variation: {coefficientOfVariation:F2}");
    Console.WriteLine($"  Randomization quality: {(coefficientOfVariation < 1.0 ? "Good" : "Highly varied")} (lower is more even)");
    
    // Conclusion
    if (uniqueRowsSelected > dataRows.Count * 0.5) // More than 50% of rows selected
    {
        Console.WriteLine($"\n✓ RANDOMIZATION WORKING: Selected from {uniqueRowsSelected} different rows out of {dataRows.Count} available");
    }
    else
    {
        Console.WriteLine($"\n⚠ LIMITED RANDOMIZATION: Only selected from {uniqueRowsSelected} different rows out of {dataRows.Count} available");
    }
    
    return 0;
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
    return 1;
}
