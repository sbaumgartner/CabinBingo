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
type BingoCell = { slotId: string; text: string; isFixedCenter: boolean };
type BingoCard = { cells: BingoCell[] };
type BingoCardsResponse = { card1: BingoCard; card2: BingoCard };

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
  return <p>Signing you in…</p>;
}

function BingoGrid({ card }: { card: BingoCard }) {
  return (
    <div className="bingo-grid">
      {card.cells.map((c, i) => (
        <div key={i} className={`bingo-cell${c.isFixedCenter ? " center" : ""}`}>
          {c.text}
        </div>
      ))}
    </div>
  );
}

export default function App() {
  const location = useLocation();
  const [userLabel, setUserLabel] = useState<string | null>(null);
  const [err, setErr] = useState<string | null>(null);
  const [profile, setProfile] = useState<ProfileResponse | null>(null);
  const [guests, setGuests] = useState<GuestDto[]>([]);
  const [selectedGuest, setSelectedGuest] = useState("");
  const [catalog, setCatalog] = useState<PrefCatalogItem[]>([]);
  const [answers, setAnswers] = useState<Record<string, string[]>>({});
  const [bingo, setBingo] = useState<BingoCardsResponse | null>(null);
  const [loading, setLoading] = useState(false);

  const refreshUser = useCallback(async () => {
    const u = await userManager.getUser();
    setUserLabel(u?.profile?.email ?? u?.profile?.name ?? u?.profile?.sub ?? null);
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
    } catch (e: unknown) {
      setErr(String(e));
    }
  }, []);

  useEffect(() => {
    void refreshUser();
    void userManager.getUser().then((u) => {
      if (u) void loadProtected();
    });
  }, [location.pathname, refreshUser, loadProtected]);

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
    setLoading(true);
    setErr(null);
    try {
      const res = await apiPostJson<BingoCardsResponse>("/bingo/cards", { seed: String(Date.now()) });
      setBingo(res);
    } catch (e: unknown) {
      setErr(String(e));
    } finally {
      setLoading(false);
    }
  };

  const prefReady = useMemo(() => {
    if (catalog.length === 0) return false;
    return catalog.every((c) => (answers[c.preferenceId]?.length ?? 0) > 0);
  }, [catalog, answers]);

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
          path="*"
          element={
            <>
              {err ? <p className="error card">{err}</p> : null}

              {!userLabel ? (
                <p className="card">Use the sign-in button above to continue.</p>
              ) : !profile ? (
                <p className="card">Loading…</p>
              ) : (
                <>
                  {!profile.onboardingComplete ? (
                    <section className="card">
                      <h2>Who are you at the cabin?</h2>
                      <p>Pick your name from the list. Each name can only be chosen once.</p>
                      <select value={selectedGuest} onChange={(e) => setSelectedGuest(e.target.value)}>
                        <option value="">Select…</option>
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

                      <section className="card">
                        <h2>Bingo</h2>
                        <button type="button" disabled={loading || !prefReady} onClick={() => void generateBingo()}>
                          Generate two cards
                        </button>
                        {bingo ? (
                          <div style={{ marginTop: "1rem" }}>
                            <h3>Card 1</h3>
                            <BingoGrid card={bingo.card1} />
                            <h3>Card 2</h3>
                            <BingoGrid card={bingo.card2} />
                          </div>
                        ) : null}
                      </section>
                    </>
                  )}
                </>
              )}

              <p style={{ marginTop: "2rem", fontSize: "0.85rem" }}>
                <Link to="/">Home</Link> · OAuth callback: <code>/callback</code>
              </p>
            </>
          }
        />
      </Routes>
    </>
  );
}
