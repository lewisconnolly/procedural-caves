using UnityEngine;
using System;

public class QuadraticErrorFunction : MonoBehaviour
{
    private const float TinyNumber = 1e-20f;
    private const int SVDNumSweeps = 5;

    private static float Abs(float x) => Math.Abs(x);
    private static float Sqrt(float x) => Mathf.Sqrt(x);
    private static float Max(float x, float y) => Mathf.Max(x, y);
    private static float Rsqrt(float x) => 1.0f / Mathf.Sqrt(x);

    private static void GivensCoeffsSym(float a_pp, float a_pq, float a_qq, out float c, out float s)
    {
        if (a_pq == 0.0f)
        {
            c = 1.0f;
            s = 0.0f;
            return;
        }
        float tau = (a_qq - a_pp) / (2.0f * a_pq);
        float stt = Sqrt(1.0f + tau * tau);
        float tan = 1.0f / ((tau >= 0.0f) ? (tau + stt) : (tau - stt));
        c = Rsqrt(1.0f + tan * tan);
        s = tan * c;
    }

    private static void SvdRotateXY(ref float x, ref float y, float c, float s)
    {
        float u = x;
        float v = y;
        x = c * u - s * v;
        y = s * u + c * v;
    }

    private static void SvdRotateqXY(ref float x, ref float y, ref float a, float c, float s)
    {
        float cc = c * c;
        float ss = s * s;
        float mx = 2.0f * c * s * a;
        float u = x;
        float v = y;
        x = cc * u - mx + ss * v;
        y = ss * u + mx + cc * v;
    }

    private static void SvdRotate(ref Matrix4x4 vtav, ref Matrix4x4 v, int a, int b)
    {
        if (vtav[a, b] == 0.0f) return;

        float c, s;
        GivensCoeffsSym(vtav[a, a], vtav[a, b], vtav[b, b], out c, out s);
        float x = vtav[a, a];
        float y = vtav[b, b];
        float z = vtav[a, b];
        SvdRotateqXY(ref x, ref y, ref z, c, s);
        vtav[a, a] = x;
        vtav[b, b] = y;
        vtav[a, b] = z;

        x = vtav[0, 3 - b];
        y = vtav[1 - a, 2];
        SvdRotateXY(ref x, ref y, c, s);
        vtav[0, 3 - b] = x;
        vtav[1 - a, 2] = y;

        vtav[a, b] = 0.0f;

        for (int i = 0; i < 3; i++)
        {
            x = v[i, a];
            y = v[i, b];
            SvdRotateXY(ref x, ref y, c, s);
            v[i, a] = x;
            v[i, b] = y;
        }
    }

    public static void SvdSolveSym(Matrix4x4 a, out Vector3 sigma, ref Matrix4x4 v)
    {
        Matrix4x4 vtav = a;
        for (int i = 0; i < SVDNumSweeps; ++i)
        {
            SvdRotate(ref vtav, ref v, 0, 1);
            SvdRotate(ref vtav, ref v, 0, 2);
            SvdRotate(ref vtav, ref v, 1, 2);
        }
        sigma = new Vector3(vtav[0, 0], vtav[1, 1], vtav[2, 2]);
    }

    private static float SvdInvdet(float x, float tol)
    {
        return (Abs(x) < tol || Abs(1.0f / x) < tol) ? 0.0f : (1.0f / x);
    }

    public static Matrix4x4 SvdPseudoinverse(Vector3 sigma, Matrix4x4 v)
    {
        float d0 = SvdInvdet(sigma.x, TinyNumber);
        float d1 = SvdInvdet(sigma.y, TinyNumber);
        float d2 = SvdInvdet(sigma.z, TinyNumber);
        return new Matrix4x4(
            v.GetColumn(0) * d0 * v[0, 0] + v.GetColumn(1) * d1 * v[0, 1] + v.GetColumn(2) * d2 * v[0, 2],
            v.GetColumn(0) * d0 * v[1, 0] + v.GetColumn(1) * d1 * v[1, 1] + v.GetColumn(2) * d2 * v[1, 2],
            v.GetColumn(0) * d0 * v[2, 0] + v.GetColumn(1) * d1 * v[2, 1] + v.GetColumn(2) * d2 * v[2, 2],
            Vector4.zero
        );
    }

    public static void SvdSolveATAATb(Matrix4x4 ATA, Vector3 ATb, out Vector3 x)
    {
        Matrix4x4 V = Matrix4x4.identity;
        Vector3 sigma;

        SvdSolveSym(ATA, out sigma, ref V);

        // A = UEV^T; U = A / (E*V^T)
        Matrix4x4 SigmaVT = Matrix4x4.zero;
        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                SigmaVT[i, j] = sigma[i] * V[j, i]; // Note the transpose of V here
            }
        }

        Matrix4x4 U = ATA * SigmaVT.inverse;

        // Now solve x = V * Σ^(-1) * U^T * ATb
        Vector3 invSigma = new Vector3(
            sigma.x != 0 ? 1f / sigma.x : 0,
            sigma.y != 0 ? 1f / sigma.y : 0,
            sigma.z != 0 ? 1f / sigma.z : 0
        );

        x = V * Vector3.Scale(invSigma, U.transpose * ATb);
    }

    public static Vector3 SvdVmulSym(Matrix4x4 a, Vector3 v)
    {
        return new Vector3(
            Vector3.Dot(a.GetRow(0), v),
            a[0, 1] * v.x + a[1, 1] * v.y + a[1, 2] * v.z,
            a[0, 2] * v.x + a[1, 2] * v.y + a[2, 2] * v.z
        );
    }

    public static Matrix4x4 SvdMulAtaSym(Matrix4x4 a)
    {
        Matrix4x4 o = Matrix4x4.zero;
        o[0, 0] = a[0, 0] * a[0, 0] + a[1, 0] * a[1, 0] + a[2, 0] * a[2, 0];
        o[0, 1] = a[0, 0] * a[0, 1] + a[1, 0] * a[1, 1] + a[2, 0] * a[2, 1];
        o[0, 2] = a[0, 0] * a[0, 2] + a[1, 0] * a[1, 2] + a[2, 0] * a[2, 2];
        o[1, 1] = a[0, 1] * a[0, 1] + a[1, 1] * a[1, 1] + a[2, 1] * a[2, 1];
        o[1, 2] = a[0, 1] * a[0, 2] + a[1, 1] * a[1, 2] + a[2, 1] * a[2, 2];
        o[2, 2] = a[0, 2] * a[0, 2] + a[1, 2] * a[1, 2] + a[2, 2] * a[2, 2];
        return o;
    }

    public static void SvdSolveAxB(Matrix4x4 a, Vector3 b, out Matrix4x4 ATA, out Vector3 ATb, out Vector3 x)
    {
        ATA = SvdMulAtaSym(a);
        ATb = a.transpose.MultiplyVector(b);
        SvdSolveATAATb(ATA, ATb, out x);
    }

    // QEF methods
    public static void QefAdd(Vector3 n, Vector3 p, ref Matrix4x4 ATA, ref Vector3 ATb, ref Vector4 pointAccum)
    {
        ATA[0, 0] += n.x * n.x;
        ATA[0, 1] += n.x * n.y;
        ATA[0, 2] += n.x * n.z;
        ATA[1, 1] += n.y * n.y;
        ATA[1, 2] += n.y * n.z;
        ATA[2, 2] += n.z * n.z;

        float b = Vector3.Dot(p, n);
        ATb += n * b;
        pointAccum += new Vector4(p.x, p.y, p.z, 1.0f);
    }

    public static float QefCalcError(Matrix4x4 A, Vector3 x, Vector3 b)
    {
        Vector3 vtmp = b - SvdVmulSym(A, x);
        return Vector3.Dot(vtmp, vtmp);
    }

    public static float QefSolve(Matrix4x4 ATA, Vector3 ATb, Vector4 pointAccum, out Vector3 x)
    {        
        Vector3 massPoint = new Vector3(pointAccum.x, pointAccum.y, pointAccum.z) / pointAccum.w;
        ATb -= SvdVmulSym(ATA, massPoint);
        SvdSolveATAATb(ATA, ATb, out x);
        float result = QefCalcError(ATA, x, ATb);

        x += massPoint;

        return result;
    }
}