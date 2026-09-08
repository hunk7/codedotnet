import { describe, expect, it } from 'vitest'
import { estimateComplexity } from '../complexity'

describe('estimateComplexity', () => {
  it('reports O(n^2)/O(1) for a simple nested loop', () => {
    const source = `
using System;

class Program
{
    static void Main()
    {
        int n = 10;
        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
            {
                Console.WriteLine(i * j);
            }
        }
    }
}
`
    const result = estimateComplexity(source)
    expect(result.time).toBe('O(n^2)')
    expect(result.space).toBe('O(1)')
  })

  it('still detects nested loops when the outer loop header is long', () => {
    const source = `
using System;

class Program
{
    static void Main()
    {
        int[] someReallyLongArrayVariableName = new int[10];
        for (int i = 0; i < someReallyLongArrayVariableName.Length; i++)
        {
            for (int j = 0; j < someReallyLongArrayVariableName.Length; j++)
            {
                Console.WriteLine(someReallyLongArrayVariableName[i] + someReallyLongArrayVariableName[j]);
            }
        }
    }
}
`
    const result = estimateComplexity(source)
    expect(result.time).toBe('O(n^2)')
  })

  it('reports O(n) for two sequential (non-nested) loops', () => {
    const source = `
using System;

class Program
{
    static void Main()
    {
        int n = 10;
        for (int i = 0; i < n; i++)
        {
            Console.WriteLine(i);
        }

        for (int j = 0; j < n; j++)
        {
            Console.WriteLine(j);
        }
    }
}
`
    const result = estimateComplexity(source)
    expect(result.time).toBe('O(n)')
  })

  it('reports O(n^3) for triple nested loops', () => {
    const source = `
using System;

class Program
{
    static void Main()
    {
        int n = 10;
        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
            {
                for (int k = 0; k < n; k++)
                {
                    Console.WriteLine(i + j + k);
                }
            }
        }
    }
}
`
    const result = estimateComplexity(source)
    expect(result.time).toBe('O(n^3)')
  })

  it('reports O(n^2) for a nested loop where the inner loop has a non-braced (single-statement) body', () => {
    const source = `using System;
using System.Collections.Generic;
using System.Linq;

public class Program
{
\tpublic static void Main()
\t{
\t\t// Print Star pattern in reverse
\t\tfor (int i = 9; i > 1; --i)
\t\t{
\t\t\tfor (int j = 1; j < i; j++)
\t\t\t\tConsole.Write("*");
\t\t\tConsole.WriteLine();
\t\t}
\t}
}
`
    const result = estimateComplexity(source)
    expect(result.time).toBe('O(n^2)')
    expect(result.rationale).toContain('two nested loops')
  })

  it('reports O(n^2) for a doubly non-braced nested loop', () => {
    const source = `
using System;

class Program
{
    static void Main()
    {
        int n = 10;
        for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
                Console.WriteLine(i + j);
    }
}
`
    const result = estimateComplexity(source)
    expect(result.time).toBe('O(n^2)')
  })
})
