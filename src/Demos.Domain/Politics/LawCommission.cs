namespace Demos.Domain.Politics;

/// <summary>Ce que propose un brouillon : une nouvelle loi, de nouveaux curseurs pour une loi en vigueur, ou son abrogation.</summary>
public enum LawChangeKind
{
    Enact,
    Amend,
    Repeal,
}

/// <summary>
/// Brouillon de la commission des lois. Pour changer ou abroger une loi en vigueur, la loi du brouillon garde l'identifiant
/// de celle-ci.
/// </summary>
public sealed record LawDraft(string Id, LawChangeKind Kind, Law Law);

/// <summary>Un brouillon voté, en attente du 1er du mois, et la légitimité payée pour lui.</summary>
public sealed record VotedLawChange(LawDraft Draft, IReadOnlyDictionary<Orientation, double> Paid);

/// <summary>Pourquoi un vote n'a pas eu lieu.</summary>
public enum LawVoteOutcome
{
    Voted,
    UnknownDraft,
    Invalid,
    UnknownLevel,
    LockedAbove,
    AlreadyVoted,
    NotEnoughLegitimacy,
}

/// <summary>
/// La commission des lois : les brouillons, gratuits, et les changements votés qui entrent en vigueur le 1er du mois
/// suivant. Voter paie la légitimité tout de suite ; annuler avant le 1er du mois la rembourse entièrement.
/// </summary>
public sealed class LawCommission
{
    private readonly List<LawDraft> _drafts = [];
    private readonly List<VotedLawChange> _voted = [];
    private int _nextNumber = 1;

    public IReadOnlyList<LawDraft> Drafts => _drafts;
    public IReadOnlyList<VotedLawChange> Voted => _voted;

    /// <summary>Un identifiant neuf, pour un brouillon ou une loi (« brouillon-3 », « loi-3 »).</summary>
    public string NewId(string prefix) => $"{prefix}-{_nextNumber++}";

    public LawDraft? DraftOf(string draftId) => _drafts.FirstOrDefault(d => d.Id == draftId);

    public void Add(LawDraft draft) => _drafts.Add(draft);

    /// <summary>Remplace la loi d'un brouillon (curseurs, sujets, niveau). Renvoie false si le brouillon n'existe pas.</summary>
    public bool Update(string draftId, Law law)
    {
        var index = _drafts.FindIndex(d => d.Id == draftId);
        if (index < 0)
            return false;

        _drafts[index] = _drafts[index] with { Law = law with { Id = _drafts[index].Law.Id } };
        return true;
    }

    public bool Remove(string draftId) => _drafts.RemoveAll(d => d.Id == draftId) > 0;

    /// <summary>Passe un brouillon dans les changements votés.</summary>
    public void MarkVoted(LawDraft draft, IReadOnlyDictionary<Orientation, double> paid)
    {
        _drafts.Remove(draft);
        _voted.Add(new VotedLawChange(draft, paid));
    }

    /// <summary>Retire un changement voté et le remet en brouillon ; renvoie ce qu'il avait coûté, ou null.</summary>
    public VotedLawChange? Unvote(string draftId)
    {
        var voted = _voted.FirstOrDefault(v => v.Draft.Id == draftId);
        if (voted is null)
            return null;

        _voted.Remove(voted);
        _drafts.Add(voted.Draft);
        return voted;
    }

    /// <summary>Les changements votés, retirés de la commission : ils entrent en vigueur.</summary>
    public List<VotedLawChange> TakeVoted()
    {
        var taken = _voted.ToList();
        _voted.Clear();
        return taken;
    }
}
