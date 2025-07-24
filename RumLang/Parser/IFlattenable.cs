namespace RumLang.Parser;

public interface IFlattenable
{
    /// <summary>
    /// This method is used to flatten the expression into a string representation.
    /// For example, a member access expression which goes 50 levels deep with member accesses, will return a single string "a.b.c.d.e.f.g.h.i.j.k.l.m.n.o.p.q.r.s.t.u.v.w.x.y.z".
    /// </summary>
    public string Flatten();
}