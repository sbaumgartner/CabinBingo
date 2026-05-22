import { useCallback, useEffect, useMemo, useState } from "react";
import { Link, Route, Routes, useLocation, useNavigate } from "react-router-dom";
import { apiGet, apiPostJson, apiPutJson } from "./api";
import { userManager } from "./auth/userManager";
import { config } from "./config";

type GuestDto = { guestId: string; displayName: string; sortOrder: number; claimedByOther: boolean };
type ProfileResponse = { guestId: string | null; guestDisplayName: string | null; onboardingComplete: boolean };
type PrefOption = { value: string; label: string };
type PrefCatalogItem = {
  preferenceId: string;
  question: string;
  answerType: string;
  options: PrefOption[];
  sortOrder: number;
};
type BingoCell = { slotId: string; text: string; isFixedCenter: boolean; preferenceLabel?: string | null };
type BingoCard = { cells: BingoCell[] };
type BingoStateResponse = {
  seed: string;
  generatedAt: string;
  card1: BingoCard;
  card2: BingoCard;
  markedCard1: boolean[];
  markedCard2: boolean[];
};
type AdminUserOverview = {
  userSub: string;
  hasProfile: boolean;
  guestId: string | null;
  guestDisplayName: string | null;
  hasBingoCards: boolean;
  bingoGeneratedAt: string | null;
  card1MarkedCount: number;
  card2MarkedCount: number;
  card1TotalCount: number;
  card2TotalCount: number;
};
type AdminUserBingoDetail = {
  userSub: string;
  guestId: string | null;
  guestDisplayName: string | null;
  generatedAt: string;
  card1: BingoCard;
  card2: BingoCard;
  markedCard1: boolean[];
  markedCard2: boolean[];
};

const cabinLetters = ["C", "A", "B", "I", "N"];

function CallbackPage() {
  const navigate = useNavigate();
  const [err, setErr] = useState<string | null>(null);

  useEffect(() => {
    userManager
      .signinRedirectCallback()
      .then(() => navigate("/", { replace: true }))
      .catch((e: unknown) => setErr(String(e)));
  }, [navigate]);

  if (err) return <p className="error">{err}</p>;
  return <p>Signing you in...</p>;
}

function BingoGrid({
  card,
  marked,
  onToggle,
  readOnly = false,
}: {
  card: BingoCard;
  marked: boolean[];
  onToggle: (index: number) => void;
  readOnly?: boolean;
}) {
  return (
    <div className="bingo-card-scroll">
      <section className="bingo-card-sheet">
        <div className="bingo-title-row" aria-hidden="true">
          {cabinLetters.map((letter) => (
            <div key={letter} className="bingo-title-cell">
              {letter}
            </div>
          ))}
        </div>
        <div className="bingo-grid">
          {card.cells.map((cell, index) => (
            <button
              key={`${cell.slotId}-${index}`}
              type="button"
              className={`bingo-cell${cell.isFixedCenter ? " center" : ""}${marked[index] ? " marked" : ""}${readOnly ? " read-only" : ""}`}
              onClick={() => {
                if (!readOnly) onToggle(index);
              }}
              aria-pressed={marked[index]}
              disabled={readOnly}
            >
              <span className="bingo-cell-content">
                {cell.preferenceLabel ? <span className="bingo-cell-label">({cell.preferenceLabel})</span> : null}
                <span className="bingo-cell-text">{cell.text}</span>
              </span>
              <span className="bingo-cell-mark" aria-hidden="true">
                {marked[index] ? "X" : ""}
              </span>
            </button>
          ))}
        </div>
      </section>
    </div>
  );
}

function formatDateTime(value: string | null) {
  if (!value) return "Not generated";
  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) return value;
  return parsed.toLocaleString();
}

function AdminPage({
  userLabel,
  err,
  loading,
  users,
  selectedUserSub,
  selectedCards,
  onViewCards,
  onSignIn,
  onSignOut,
}: {
  userLabel: string | null;
  err: string | null;
  loading: boolean;
  users: AdminUserOverview[];
  selectedUserSub: string | null;
  selectedCards: AdminUserBingoDetail | null;
  onViewCards: (userSub: string) => void;
  onSignIn: () => void;
  onSignOut: () => void;
}) {
  return (
    <>
      <header className="card">
        <h1 style={{ marginTop: 0 }}>Cabin Bingo Admin</h1>
        <p style={{ marginBottom: 0 }}>
          {userLabel ? (
            <>
              Signed in as <strong>{userLabel}</strong> ·{" "}
              <button type="button" className="secondary" onClick={onSignOut}>
                Sign out
              </button>
            </>
          ) : (
            <button type="button" onClick={onSignIn}>
              Sign in with Cognito
            </button>
          )}
        </p>
      </header>

      {err ? <p className="error card">{err}</p> : null}

      {!userLabel ? (
        <p className="card">Sign in to view the admin screen.</p>
      ) : loading && users.length === 0 ? (
        <p className="card">Loading...</p>
      ) : (
        <section className="card">
          <h2>Users and Bingo Status</h2>
          <div className="admin-table-wrap">
            <table className="admin-table">
              <thead>
                <tr>
                  <th>Guest</th>
                  <th>Profile</th>
                  <th>Cards</th>
                  <th>Card 1</th>
                  <th>Card 2</th>
                  <th>Generated</th>
                  <th>User Sub</th>
                  <th>Cards</th>
                </tr>
              </thead>
              <tbody>
                {users.map((user) => (
                  <tr key={user.userSub}>
                    <td>{user.guestDisplayName ?? "Unclaimed"}</td>
                    <td>{user.hasProfile ? "Yes" : "No"}</td>
                    <td>{user.hasBingoCards ? "Generated" : "None"}</td>
                    <td>
                      {user.hasBingoCards ? `${user.card1MarkedCount}/${user.card1TotalCount}` : "-"}
                    </td>
                    <td>
                      {user.hasBingoCards ? `${user.card2MarkedCount}/${user.card2TotalCount}` : "-"}
                    </td>
                    <td>{formatDateTime(user.bingoGeneratedAt)}</td>
                    <td className="admin-sub">{user.userSub}</td>
                    <td>
                      <button
                        type="button"
                        className="secondary"
                        disabled={!user.hasBingoCards || loading}
                        onClick={() => onViewCards(user.userSub)}
                      >
                        {selectedUserSub === user.userSub ? "Viewing" : "View cards"}
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          {selectedCards ? (
            <section className="admin-cards-panel">
              <div className="admin-cards-header">
                <div>
                  <h3>{selectedCards.guestDisplayName ?? selectedCards.userSub}</h3>
                  <p>Generated {formatDateTime(selectedCards.generatedAt)}</p>
                </div>
              </div>
              <div className="bingo-cards">
                <div className="bingo-card-block">
                  <div className="bingo-card-heading">
                    <h3>Card 1</h3>
                    <span>
                      {selectedCards.markedCard1.filter(Boolean).length}/{selectedCards.markedCard1.length} marked
                    </span>
                  </div>
                  <BingoGrid
                    card={selectedCards.card1}
                    marked={selectedCards.markedCard1}
                    onToggle={() => {}}
                    readOnly
                  />
                </div>
                <div className="bingo-card-block">
                  <div className="bingo-card-heading">
                    <h3>Card 2</h3>
                    <span>
                      {selectedCards.markedCard2.filter(Boolean).length}/{selectedCards.markedCard2.length} marked
                    </span>
                  </div>
                  <BingoGrid
                    card={selectedCards.card2}
                    marked={selectedCards.markedCard2}
                    onToggle={() => {}}
                    readOnly
                  />
                </div>
              </div>
            </section>
          ) : null}
        </section>
      )}
    </>
  );
}

export default function App() {
  const location = useLocation();
  const isAdminRoute = location.pathname === "/admin";
  const [userLabel, setUserLabel] = useState<string | null>(null);
  const [err, setErr] = useState<string | null>(null);
  const [profile, setProfile] = useState<ProfileResponse | null>(null);
  const [guests, setGuests] = useState<GuestDto[]>([]);
  const [selectedGuest, setSelectedGuest] = useState("");
  const [catalog, setCatalog] = useState<PrefCatalogItem[]>([]);
  const [answers, setAnswers] = useState<Record<string, string[]>>({});
  const [bingo, setBingo] = useState<BingoStateResponse | null>(null);
  const [markedCards, setMarkedCards] = useState<[boolean[], boolean[]] | null>(null);
  const [adminUsers, setAdminUsers] = useState<AdminUserOverview[]>([]);
  const [selectedAdminUserSub, setSelectedAdminUserSub] = useState<string | null>(null);
  const [selectedAdminCards, setSelectedAdminCards] = useState<AdminUserBingoDetail | null>(null);
  const [loading, setLoading] = useState(false);
  const [savingMarksCount, setSavingMarksCount] = useState(0);

  const refreshUser = useCallback(async () => {
    const u = await userManager.getUser();
    if (!u || u.expired) {
      setUserLabel(null);
      return;
    }

    setUserLabel(u.profile?.email ?? u.profile?.name ?? u.profile?.sub ?? null);
  }, []);

  const loadProtected = useCallback(async () => {
    setErr(null);
    try {
      const [p, g, c] = await Promise.all([
        apiGet<ProfileResponse>("/profile"),
        apiGet<GuestDto[]>("/guests"),
        apiGet<PrefCatalogItem[]>("/preferences/catalog"),
      ]);
      setProfile(p);
      setGuests(g);
      setCatalog(c);
      const existing = await apiGet<Record<string, string[]>>("/preferences/me");
      setAnswers(existing);
      const savedBingo = await apiGet<BingoStateResponse | null>("/bingo/me");
      setBingo(savedBingo);
      setMarkedCards(savedBingo ? [savedBingo.markedCard1, savedBingo.markedCard2] : null);
    } catch (e: unknown) {
      setErr(String(e));
    }
  }, []);

  const loadAdmin = useCallback(async () => {
    setErr(null);
    setLoading(true);
    try {
      const users = await apiGet<AdminUserOverview[]>("/admin/overview");
      setAdminUsers(users);
    } catch (e: unknown) {
      setErr(String(e));
    } finally {
      setLoading(false);
    }
  }, []);

  const loadAdminCards = useCallback(async (userSub: string) => {
    setErr(null);
    setLoading(true);
    setSelectedAdminUserSub(userSub);
    try {
      const cards = await apiGet<AdminUserBingoDetail>(`/admin/users/${encodeURIComponent(userSub)}/bingo`);
      setSelectedAdminCards(cards);
    } catch (e: unknown) {
      setErr(String(e));
      setSelectedAdminCards(null);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void refreshUser();
    void userManager.getUser().then((u) => {
      if (!u || u.expired) return;
      if (isAdminRoute) void loadAdmin();
      else void loadProtected();
    });
  }, [isAdminRoute, location.pathname, refreshUser, loadProtected, loadAdmin]);

  const signIn = () => {
    void userManager.signinRedirect();
  };

  const signOut = () => {
    void userManager.signoutRedirect({
      post_logout_redirect_uri: config.postLogoutRedirectUri,
    });
  };

  const saveProfile = async () => {
    if (!selectedGuest) return;
    setLoading(true);
    setErr(null);
    try {
      await apiPutJson("/profile", { guestId: selectedGuest });
      await loadProtected();
    } catch (e: unknown) {
      setErr(String(e));
    } finally {
      setLoading(false);
    }
  };

  const toggleMulti = (prefId: string, value: string, checked: boolean) => {
    setAnswers((prev) => {
      const cur = new Set(prev[prefId] ?? []);
      if (checked) cur.add(value);
      else cur.delete(value);
      return { ...prev, [prefId]: [...cur] };
    });
  };

  const setSingle = (prefId: string, value: string) => {
    setAnswers((prev) => ({ ...prev, [prefId]: [value] }));
  };

  const savePreferences = async () => {
    setLoading(true);
    setErr(null);
    try {
      await apiPutJson("/preferences/me", { answers });
      await loadProtected();
    } catch (e: unknown) {
      setErr(String(e));
    } finally {
      setLoading(false);
    }
  };

  const generateBingo = async () => {
    if (bingo) {
      const confirmed = window.confirm(
        "Generating new cards will delete your current cards and all marked progress. Continue?",
      );
      if (!confirmed) return;
    }

    setLoading(true);
    setErr(null);
    try {
      const res = await apiPostJson<BingoStateResponse>("/bingo/cards", {
        seed: String(Date.now()),
        replaceExisting: bingo !== null,
      });
      setBingo(res);
      setMarkedCards([
        [...res.markedCard1],
        [...res.markedCard2],
      ]);
    } catch (e: unknown) {
      setErr(String(e));
    } finally {
      setLoading(false);
    }
  };

  const saveBingoProgress = useCallback(async (next: [boolean[], boolean[]]) => {
    setSavingMarksCount((count) => count + 1);
    try {
      await apiPutJson("/bingo/me", {
        markedCard1: next[0],
        markedCard2: next[1],
      });
    } catch (e: unknown) {
      setErr(String(e));
    } finally {
      setSavingMarksCount((count) => Math.max(0, count - 1));
    }
  }, []);

  const toggleMarked = (cardIndex: 0 | 1, cellIndex: number) => {
    setErr(null);
    setMarkedCards((prev) => {
      if (!prev) return prev;

      const next: [boolean[], boolean[]] = [[...prev[0]], [...prev[1]]];
      next[cardIndex][cellIndex] = !next[cardIndex][cellIndex];
      void saveBingoProgress(next);
      return next;
    });
  };

  const prefReady = useMemo(() => {
    if (catalog.length === 0) return false;
    return catalog.every((c) => (answers[c.preferenceId]?.length ?? 0) > 0);
  }, [catalog, answers]);

  const savingMarks = savingMarksCount > 0;

  const printCards = () => {
    window.print();
  };

  return (
    <>
      <header className="card">
        <h1 style={{ marginTop: 0 }}>Cabin Bingo</h1>
        <p style={{ marginBottom: 0 }}>
          {userLabel ? (
            <>
              Signed in as <strong>{userLabel}</strong> ·{" "}
              <button type="button" className="secondary" onClick={signOut}>
                Sign out
              </button>
            </>
          ) : (
            <button type="button" onClick={signIn}>
              Sign in with Cognito
            </button>
          )}
        </p>
      </header>

      <Routes>
        <Route path="/callback" element={<CallbackPage />} />
        <Route
          path="/admin"
          element={
            <AdminPage
              userLabel={userLabel}
              err={err}
              loading={loading}
              users={adminUsers}
              selectedUserSub={selectedAdminUserSub}
              selectedCards={selectedAdminCards}
              onViewCards={(userSub) => void loadAdminCards(userSub)}
              onSignIn={signIn}
              onSignOut={signOut}
            />
          }
        />
        <Route
          path="*"
          element={
            <>
              {err ? <p className="error card">{err}</p> : null}

              {!userLabel ? (
                <p className="card">Use the sign-in button above to continue.</p>
              ) : !profile ? (
                <p className="card">Loading...</p>
              ) : (
                <>
                  {!profile.onboardingComplete ? (
                    <section className="card">
                      <h2>Who are you at the cabin?</h2>
                      <p>Pick your name from the list. Each name can only be chosen once.</p>
                      <select value={selectedGuest} onChange={(e) => setSelectedGuest(e.target.value)}>
                        <option value="">Select...</option>
                        {guests.map((g) => (
                          <option key={g.guestId} value={g.guestId}>
                            {g.displayName}
                          </option>
                        ))}
                      </select>
                      <p>
                        <button type="button" disabled={loading || !selectedGuest} onClick={() => void saveProfile()}>
                          Save
                        </button>
                      </p>
                    </section>
                  ) : (
                    <>
                      <section className="card">
                        <h2>Preferences</h2>
                        <p>
                          Logged in as cabin guest: <strong>{profile.guestDisplayName}</strong>
                        </p>
                        {catalog.map((c) => (
                          <div key={c.preferenceId}>
                            <div style={{ fontWeight: 600 }}>{c.question}</div>
                            {c.answerType.toLowerCase() === "multi" ? (
                              c.options.map((o) => (
                                <label key={o.value} className="pref">
                                  <input
                                    type="checkbox"
                                    checked={(answers[c.preferenceId] ?? []).includes(o.value)}
                                    onChange={(e) => toggleMulti(c.preferenceId, o.value, e.target.checked)}
                                  />{" "}
                                  {o.label}
                                </label>
                              ))
                            ) : (
                              c.options.map((o) => (
                                <label key={o.value} className="pref">
                                  <input
                                    type="radio"
                                    name={c.preferenceId}
                                    checked={(answers[c.preferenceId]?.[0] ?? "") === o.value}
                                    onChange={() => setSingle(c.preferenceId, o.value)}
                                  />{" "}
                                  {o.label}
                                </label>
                              ))
                            )}
                          </div>
                        ))}
                        <p>
                          <button type="button" disabled={loading || !prefReady} onClick={() => void savePreferences()}>
                            Save preferences
                          </button>
                        </p>
                      </section>

                      <section className="card bingo-panel">
                        <h2>Bingo</h2>
                        <div className="bingo-actions">
                          <button type="button" disabled={loading || !prefReady} onClick={() => void generateBingo()}>
                            {bingo ? "Generate new cards" : "Generate two cards"}
                          </button>
                          <button type="button" className="secondary" disabled={!bingo} onClick={printCards}>
                            Print cards
                          </button>
                        </div>
                        {bingo ? (
                          <div className="bingo-results">
                            <p className="bingo-note">
                              Tap a square to mark it off. Generating new cards replaces both cards and clears all marks.
                            </p>
                            <p className="bingo-save-status" aria-live="polite">
                              {savingMarks ? "Saving progress..." : "Progress saved"}
                            </p>
                            <div className="bingo-cards">
                              <div className="bingo-card-block">
                                <div className="bingo-card-heading">
                                  <h3>Card 1</h3>
                                  <span>{markedCards?.[0].filter(Boolean).length ?? 0} marked</span>
                                </div>
                                <BingoGrid
                                  card={bingo.card1}
                                  marked={markedCards?.[0] ?? bingo.card1.cells.map(() => false)}
                                  onToggle={(index) => toggleMarked(0, index)}
                                />
                              </div>
                              <div className="bingo-card-block">
                                <div className="bingo-card-heading">
                                  <h3>Card 2</h3>
                                  <span>{markedCards?.[1].filter(Boolean).length ?? 0} marked</span>
                                </div>
                                <BingoGrid
                                  card={bingo.card2}
                                  marked={markedCards?.[1] ?? bingo.card2.cells.map(() => false)}
                                  onToggle={(index) => toggleMarked(1, index)}
                                />
                              </div>
                            </div>
                          </div>
                        ) : null}
                      </section>
                    </>
                  )}
                </>
              )}

              <p className="app-footer" style={{ marginTop: "2rem", fontSize: "0.85rem" }}>
                <Link to="/">Home</Link> · OAuth callback: <code>/callback</code>
              </p>
            </>
          }
        />
      </Routes>
    </>
  );
}
