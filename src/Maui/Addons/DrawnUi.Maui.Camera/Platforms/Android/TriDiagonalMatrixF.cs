using Debug = System.Diagnostics.Debug;
using StringBuilder = System.Text.StringBuilder;

namespace DrawnUi.Camera;

public class TriDiagonalMatrixF
{
    /// <summary>
    /// The values for the sub-diagonal. A[0] is never used.
    /// </summary>
    public float[] A;

    /// <summary>
    /// The values for the main diagonal.
    /// </summary>
    public float[] B;

    /// <summary>
    /// The values for the super-diagonal. C[C.Length-1] is never used.
    /// </summary>
    public float[] C;

    /// <summary>
    /// The width and height of this matrix.
    /// </summary>
    public int N
    {
        get { return (A != null ? A.Length : 0); }
    }

    /// <summary>
    /// Indexer. Setter throws an exception if you try to set any not on the super, main, or sub diagonals.
    /// </summary>
    public float this[int row, int col]
    {
        get
        {
            int di = row - col;

            if (di == 0)
            {
                return B[row];
            }
            else if (di == -1)
            {
                Debug.Assert(row < N - 1);
                return C[row];
            }
            else if (di == 1)
            {
                Debug.Assert(row > 0);
                return A[row];
            }
            else return 0;
        }
        set
        {
            int di = row - col;

            if (di == 0)
            {
                B[row] = value;
            }
            else if (di == -1)
            {
                Debug.Assert(row < N - 1);
                C[row] = value;
            }
            else if (di == 1)
            {
                Debug.Assert(row > 0);
                A[row] = value;
            }
            else
            {
                throw new ArgumentException("Only the main, super, and sub diagonals can be set.");
            }
        }
    }

    /// <summary>
    /// Construct an NxN matrix.
    /// </summary>
    public TriDiagonalMatrixF(int n)
    {
        this.A = new float[n];
        this.B = new float[n];
        this.C = new float[n];
    }

    /// <summary>
    /// Produce a string representation of the contents of this matrix.
    /// </summary>
    /// <param name="fmt">Optional. For String.Format. Must include the colon. Examples are ':0.000' and ',5:0.00' </param>
    /// <param name="prefix">Optional. Per-line indentation prefix.</param>
    public string ToDisplayString(string fmt = "", string prefix = "")
    {
        if (this.N > 0)
        {
            var s = new StringBuilder();
            string formatString = "{0" + fmt + "}";

            for (int r = 0; r < N; r++)
            {
                s.Append(prefix);

                for (int c = 0; c < N; c++)
                {
                    s.AppendFormat(formatString, this[r, c]);
                    if (c < N - 1) s.Append(", ");
                }

                s.AppendLine();
            }

            return s.ToString();
        }
        else
        {
            return prefix + "0x0 Matrix";
        }
    }

    /// <summary>
    /// Solve the system of equations this*x=d given the specified d.
    /// </summary>
    /// <remarks>
    /// Uses the Thomas algorithm described in the wikipedia article: http://en.wikipedia.org/wiki/Tridiagonal_matrix_algorithm
    /// Not optimized. Not destructive.
    /// </remarks>
    /// <param name="d">Right side of the equation.</param>
    public float[] Solve(float[] d)
    {
        int n = this.N;

        if (d.Length != n)
        {
            throw new ArgumentException("The input d is not the same size as this matrix.");
        }

        // cPrime
        float[] cPrime = new float[n];
        cPrime[0] = C[0] / B[0];

        for (int i = 1; i < n; i++)
        {
            cPrime[i] = C[i] / (B[i] - cPrime[i - 1] * A[i]);
        }

        // dPrime
        float[] dPrime = new float[n];
        dPrime[0] = d[0] / B[0];

        for (int i = 1; i < n; i++)
        {
            dPrime[i] = (d[i] - dPrime[i - 1] * A[i]) / (B[i] - cPrime[i - 1] * A[i]);
        }

        // Back substitution
        float[] x = new float[n];
        x[n - 1] = dPrime[n - 1];

        for (int i = n - 2; i >= 0; i--)
        {
            x[i] = dPrime[i] - cPrime[i] * x[i + 1];
        }

        return x;
    }
}
