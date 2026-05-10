using System;
using System.Linq.Expressions;
using System.Reflection;

public class R
{
    public int ReadInt32() { return 0; }
    public string ReadString(int length) { return string.Empty; }
}
public static class D
{
    public static string Dump()
    {
        Expression<Func<R, Func<int>>> e1 = o => o.ReadInt32;
        Expression<Func<R, Func<int,string>>> e2 = o => o.ReadString;
        return DumpExpr(e1.Body, 0) + "\n---\n" + DumpExpr(e2.Body, 0);
    }
    static string DumpExpr(Expression e, int level)
    {
        string pad = new string(' ', level*2);
        string s = pad + e.NodeType + " " + e.GetType().FullName + " " + e.ToString() + "\n";
        UnaryExpression u = e as UnaryExpression;
        if (u != null) s += DumpExpr(u.Operand, level+1);
        MethodCallExpression m = e as MethodCallExpression;
        if (m != null)
        {
            s += pad + "  Method=" + m.Method + " Object=" + (m.Object == null ? "<null>" : m.Object.ToString()) + "\n";
            foreach (var a in m.Arguments) s += DumpExpr(a, level+1);
        }
        MemberExpression me = e as MemberExpression;
        if (me != null) s += pad + "  Member=" + me.Member + " Expr=" + (me.Expression == null ? "<null>" : me.Expression.ToString()) + "\n";
        ConstantExpression c = e as ConstantExpression;
        if (c != null) s += pad + "  Value=" + (c.Value == null ? "<null>" : c.Value + " (" + c.Value.GetType().FullName + ")") + "\n";
        return s;
    }
}
