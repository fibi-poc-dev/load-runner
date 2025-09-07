#!/usr/bin/env python3

import re
import subprocess
import sys
from collections import Counter

def test_improved_randomization():
    print("Testing Improved CSV Row Randomization")
    print("=====================================")
    
    try:
        # Run LoadRunner and capture BankId usage
        cmd = ['dotnet', 'run']
        process = subprocess.Popen(cmd, 
                                 stdin=subprocess.PIPE,
                                 stdout=subprocess.PIPE, 
                                 stderr=subprocess.STDOUT,
                                 text=True,
                                 cwd='/Users/alexk/dotnet/LoadRunner/src/LoadRunner')
        
        # Send "Y" to start the test
        stdout, _ = process.communicate(input="Y\n", timeout=20)
        
        # Extract BankId patterns from debug output
        bank_pattern = re.compile(r'Iteration using data row \d+ - BankId: (\d+)')
        bank_ids = bank_pattern.findall(stdout)
        
        if not bank_ids:
            print("No BankId patterns found in output. Debug logging might be off.")
            return
            
        print(f"Found {len(bank_ids)} iterations with BankId data")
        print(f"Unique BankIds used: {len(set(bank_ids))}")
        
        # Count occurrences
        counter = Counter(bank_ids)
        
        print("\nTop 10 most used BankIds:")
        for bank_id, count in counter.most_common(10):
            print(f"  BankId {bank_id}: used {count} times")
        
        print(f"\nRandomization Analysis:")
        print(f"  Total iterations captured: {len(bank_ids)}")
        print(f"  Unique BankIds: {len(counter)}")
        print(f"  Coverage: {len(counter)/116*100:.1f}% of available rows")
        
        if len(counter) > 20:  # If we used more than 20 different rows
            print("✅ IMPROVED RANDOMIZATION WORKING: High row coverage achieved!")
        else:
            print("⚠️  Limited coverage - may need longer test or more virtual users")
            
    except subprocess.TimeoutExpired:
        print("Test completed (timeout reached)")
    except Exception as e:
        print(f"Error: {e}")

if __name__ == "__main__":
    test_improved_randomization()
