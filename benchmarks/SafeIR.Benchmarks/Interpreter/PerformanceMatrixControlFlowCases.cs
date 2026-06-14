namespace SafeIR.Benchmarks.Interpreter;

internal static class PerformanceMatrixControlFlowCases
{
    public static IReadOnlyList<PerformanceMatrixCase> All()
        => [
            new("while i32 add/rem loop", 2_000_000, 50_000, HandwrittenWhileI32Modulo, WhileI32ModuloJson()),
            new("if branch i32 loop", 2_000_000, 50_000, HandwrittenIfBranch, IfBranchJson()),
            new("two-arg local function", 750_000, 25_000, HandwrittenTwoArgLocalCall, TwoArgLocalCallJson())
        ];

    private static object HandwrittenWhileI32Modulo(int iterations)
    {
        var i = 0;
        var total = 0;
        while (i < iterations)
        {
            total = (total + i) % 1_000_003;
            i++;
        }

        return total;
    }

    private static object HandwrittenIfBranch(int iterations)
    {
        var total = 0;
        for (var i = 0; i < iterations; i++)
        {
            if (i % 2 == 0)
            {
                total += 1;
            }
            else
            {
                total += 2;
            }
        }

        return total;
    }

    private static object HandwrittenTwoArgLocalCall(int iterations)
    {
        var total = 0;
        for (var i = 0; i < iterations; i++)
        {
            total = Add(total, i % 3);
        }

        return total;
    }

    private static int Add(int left, int right) => left + right;

    private static string WhileI32ModuloJson()
        => """
        {
          "id": "matrix-while-i32-modulo",
          "version": "1.0.0",
          "functions": [
            {
              "id": "main",
              "visibility": "entrypoint",
              "parameters": [{ "name": "iterations", "type": "I32" }],
              "returnType": "I32",
              "body": [
                { "op": "set", "name": "i", "value": { "i32": 0 } },
                { "op": "set", "name": "total", "value": { "i32": 0 } },
                {
                  "op": "while",
                  "condition": { "op": "lt", "left": { "var": "i" }, "right": { "var": "iterations" } },
                  "body": [
                    { "op": "set", "name": "total", "value": {
                      "op": "rem",
                      "left": { "op": "add", "left": { "var": "total" }, "right": { "var": "i" } },
                      "right": { "i32": 1000003 } } },
                    { "op": "set", "name": "i", "value": {
                      "op": "add",
                      "left": { "var": "i" },
                      "right": { "i32": 1 } } }
                  ]
                },
                { "op": "return", "value": { "var": "total" } }
              ]
            }
          ]
        }
        """;

    private static string IfBranchJson()
        => """
        {
          "id": "matrix-if-branch",
          "version": "1.0.0",
          "functions": [
            {
              "id": "main",
              "visibility": "entrypoint",
              "parameters": [{ "name": "iterations", "type": "I32" }],
              "returnType": "I32",
              "body": [
                { "op": "set", "name": "total", "value": { "i32": 0 } },
                {
                  "op": "forRange",
                  "local": "i",
                  "start": { "i32": 0 },
                  "end": { "var": "iterations" },
                  "body": [
                    {
                      "op": "if",
                      "condition": {
                        "op": "eq",
                        "left": { "op": "rem", "left": { "var": "i" }, "right": { "i32": 2 } },
                        "right": { "i32": 0 }
                      },
                      "then": [
                        { "op": "set", "name": "total", "value": {
                          "op": "add",
                          "left": { "var": "total" },
                          "right": { "i32": 1 } } }
                      ],
                      "else": [
                        { "op": "set", "name": "total", "value": {
                          "op": "add",
                          "left": { "var": "total" },
                          "right": { "i32": 2 } } }
                      ]
                    }
                  ]
                },
                { "op": "return", "value": { "var": "total" } }
              ]
            }
          ]
        }
        """;

    private static string TwoArgLocalCallJson()
        => """
        {
          "id": "matrix-two-arg-local-call",
          "version": "1.0.0",
          "functions": [
            {
              "id": "add",
              "visibility": "private",
              "parameters": [
                { "name": "left", "type": "I32" },
                { "name": "right", "type": "I32" }
              ],
              "returnType": "I32",
              "body": [{ "op": "return", "value": {
                "op": "add",
                "left": { "var": "left" },
                "right": { "var": "right" } } }]
            },
            {
              "id": "main",
              "visibility": "entrypoint",
              "parameters": [{ "name": "iterations", "type": "I32" }],
              "returnType": "I32",
              "body": [
                { "op": "set", "name": "total", "value": { "i32": 0 } },
                {
                  "op": "forRange",
                  "local": "i",
                  "start": { "i32": 0 },
                  "end": { "var": "iterations" },
                  "body": [
                    { "op": "set", "name": "total", "value": {
                      "call": "add",
                      "args": [
                        { "var": "total" },
                        { "op": "rem", "left": { "var": "i" }, "right": { "i32": 3 } }
                      ] } }
                  ]
                },
                { "op": "return", "value": { "var": "total" } }
              ]
            }
          ]
        }
        """;
}
