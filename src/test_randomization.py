#!/usr/bin/env python3

import csv
import random
from collections import Counter

print("CSV Row Randomization Test")
print("==========================")

try:
    # Load CSV data
    csv_path = "SampleData/referential_data.csv"
    
    with open(csv_path, 'r') as file:
        csv_reader = csv.DictReader(file)
        data_rows = list(csv_reader)
    
    print(f"Loaded {len(data_rows)} rows from CSV file")
    
    # Test randomization by simulating virtual user row selection 200 times
    selected_bank_ids = []
    selected_indices = []
    
    print("\nSimulating 200 virtual user row selections:")
    for i in range(200):
        data_row_index = random.randint(0, len(data_rows) - 1)
        data_row = data_rows[data_row_index]
        bank_id = data_row['BankId']
        
        selected_bank_ids.append(bank_id)
        selected_indices.append(data_row_index)
        
        if i < 20:  # Show first 20 selections
            print(f"  Selection {i+1:02d}: Row {data_row_index:03d} - BankId={bank_id}")
    
    print("\n... (showing first 20 selections)")
    
    # Analyze distribution
    bank_id_counts = Counter(selected_bank_ids)
    unique_rows_selected = len(bank_id_counts)
    total_selections = len(selected_bank_ids)
    avg_selections_per_row = total_selections / unique_rows_selected
    max_selections = max(bank_id_counts.values())
    min_selections = min(bank_id_counts.values())
    
    print(f"\nRandomization Analysis:")
    print(f"  Total CSV rows available: {len(data_rows)}")
    print(f"  Unique rows selected: {unique_rows_selected}")
    print(f"  Total selections made: {total_selections}")
    print(f"  Average selections per row: {avg_selections_per_row:.2f}")
    print(f"  Max selections for any row: {max_selections}")
    print(f"  Min selections for any row: {min_selections}")
    
    # Show distribution of most/least selected rows
    most_common = bank_id_counts.most_common(5)
    least_common = bank_id_counts.most_common()[-5:]
    
    print(f"\nTop 5 most selected rows:")
    for bank_id, count in most_common:
        print(f"  BankId {bank_id}: selected {count} times")
    
    print(f"\nTop 5 least selected rows:")
    for bank_id, count in reversed(least_common):
        print(f"  BankId {bank_id}: selected {count} times")
    
    # Calculate distribution evenness (coefficient of variation)
    import math
    values = list(bank_id_counts.values())
    std_dev = math.sqrt(sum((v - avg_selections_per_row) ** 2 for v in values) / len(values))
    coefficient_of_variation = std_dev / avg_selections_per_row
    
    print(f"\nDistribution Analysis:")
    print(f"  Standard deviation: {std_dev:.2f}")
    print(f"  Coefficient of variation: {coefficient_of_variation:.2f}")
    print(f"  Randomization quality: {'Good' if coefficient_of_variation < 1.0 else 'Highly varied'} (lower is more even)")
    
    # Row index distribution analysis
    index_counts = Counter(selected_indices)
    print(f"\nRow Index Distribution:")
    print(f"  Unique row indices selected: {len(index_counts)}")
    print(f"  Index range: {min(selected_indices)} to {max(selected_indices)}")
    
    # Conclusion
    if unique_rows_selected > len(data_rows) * 0.5:  # More than 50% of rows selected
        print(f"\n✓ RANDOMIZATION WORKING: Selected from {unique_rows_selected} different rows out of {len(data_rows)} available")
    else:
        print(f"\n⚠ LIMITED RANDOMIZATION: Only selected from {unique_rows_selected} different rows out of {len(data_rows)} available")
    
except Exception as e:
    print(f"Error: {e}")
    exit(1)
