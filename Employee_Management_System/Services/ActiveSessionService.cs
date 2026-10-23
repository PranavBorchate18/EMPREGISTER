
using Employee_Management_System.Models;
using System;
using System.Collections.Concurrent;

namespace Employee_Management_System.Services
{
    /// <summary>
    /// Manages active login sessions and active browser tabs.
    /// </summary>
    public class ActiveSessionService
    {
        private readonly ConcurrentDictionary<string, ActiveLoginSession> _sessions =
            new(StringComparer.OrdinalIgnoreCase);

        private readonly ConcurrentDictionary<string, TabState> _tabs =
            new(StringComparer.OrdinalIgnoreCase);

        private readonly object _gate = new();

        private sealed class TabState
        {
            public string SessionToken { get; init; } = "";
            public string TabId { get; init; } = "";
            public string Page { get; set; } = "Employee Registration";
            public DateTime LastSeenUtc { get; set; } = DateTime.UtcNow;
        }

        public sealed class TabDetails
        {
            public string TabId { get; init; } = "";
            public string Page { get; init; } = "";
            public DateTime LastSeenUtc { get; init; }
        }

        public ActiveLoginSession? Get(string loginId)
        {
            if (string.IsNullOrWhiteSpace(loginId))
                return null;

            lock (_gate)
            {
                return _sessions.TryGetValue(loginId, out var session)
                    ? session
                    : null;
            }
        }

        public bool IsCurrent(string loginId, string token)
        {
            if (string.IsNullOrWhiteSpace(loginId) ||
                string.IsNullOrWhiteSpace(token))
                return false;

            lock (_gate)
            {
                return _sessions.TryGetValue(loginId, out var session) &&
                       string.Equals(
                           session.SessionToken,
                           token,
                           StringComparison.Ordinal);
            }
        }

        public bool TryStart(
            ActiveLoginSession next,
            string? expectedOldToken,
            out ActiveLoginSession? conflict)
        {
            conflict = null;

            if (next == null ||
                string.IsNullOrWhiteSpace(next.LoginId) ||
                string.IsNullOrWhiteSpace(next.SessionToken))
            {
                return false;
            }

            lock (_gate)
            {
                _sessions.TryGetValue(next.LoginId, out conflict);

                if (conflict != null &&
                    !string.Equals(
                        conflict.SessionToken,
                        expectedOldToken,
                        StringComparison.Ordinal))
                {
                    return false;
                }

                _sessions[next.LoginId] = next;

                // A new login session must claim its tab again.
                _tabs.TryRemove(next.LoginId, out _);

                conflict = null;
                return true;
            }
        }

        public void RemoveIfCurrent(string loginId, string token)
        {
            if (string.IsNullOrWhiteSpace(loginId) ||
                string.IsNullOrWhiteSpace(token))
            {
                return;
            }

            lock (_gate)
            {
                if (_sessions.TryGetValue(loginId, out var current) &&
                    string.Equals(
                        current.SessionToken,
                        token,
                        StringComparison.Ordinal))
                {
                    _sessions.TryRemove(loginId, out _);
                    _tabs.TryRemove(loginId, out _);
                }
            }
        }

        public TabDetails? GetActiveTab(
            string loginId,
            string sessionToken)
        {
            if (string.IsNullOrWhiteSpace(loginId) ||
                string.IsNullOrWhiteSpace(sessionToken))
            {
                return null;
            }

            lock (_gate)
            {
                if (!_sessions.TryGetValue(loginId, out var session) ||
                    !string.Equals(
                        session.SessionToken,
                        sessionToken,
                        StringComparison.Ordinal) ||
                    !_tabs.TryGetValue(loginId, out var tab) ||
                    !string.Equals(
                        tab.SessionToken,
                        sessionToken,
                        StringComparison.Ordinal))
                {
                    return null;
                }

                return new TabDetails
                {
                    TabId = tab.TabId,
                    Page = tab.Page,
                    LastSeenUtc = tab.LastSeenUtc
                };
            }
        }

        public bool TryClaimTab(
            string loginId,
            string sessionToken,
            string tabId)
        {
            if (string.IsNullOrWhiteSpace(loginId) ||
                string.IsNullOrWhiteSpace(sessionToken) ||
                string.IsNullOrWhiteSpace(tabId) ||
                tabId.Length > 100)
            {
                return false;
            }

            lock (_gate)
            {
                if (!_sessions.TryGetValue(loginId, out var session) ||
                    !string.Equals(
                        session.SessionToken,
                        sessionToken,
                        StringComparison.Ordinal))
                {
                    return false;
                }

                if (_tabs.TryGetValue(loginId, out var existing))
                {
                    if (!string.Equals(
                            existing.SessionToken,
                            sessionToken,
                            StringComparison.Ordinal) ||
                        !string.Equals(
                            existing.TabId,
                            tabId,
                            StringComparison.Ordinal))
                    {
                        return false;
                    }

                    existing.LastSeenUtc = DateTime.UtcNow;
                    return true;
                }

                _tabs[loginId] = new TabState
                {
                    SessionToken = sessionToken,
                    TabId = tabId,
                    Page = "Employee Registration",
                    LastSeenUtc = DateTime.UtcNow
                };

                return true;
            }
        }

        public bool CheckTab(
            string loginId,
            string sessionToken,
            string tabId,
            string? page = null)
        {
            if (string.IsNullOrWhiteSpace(loginId) ||
                string.IsNullOrWhiteSpace(sessionToken) ||
                string.IsNullOrWhiteSpace(tabId) ||
                tabId.Length > 100)
            {
                return false;
            }

            lock (_gate)
            {
                if (!_sessions.TryGetValue(loginId, out var session) ||
                    !string.Equals(
                        session.SessionToken,
                        sessionToken,
                        StringComparison.Ordinal) ||
                    !_tabs.TryGetValue(loginId, out var active) ||
                    !string.Equals(
                        active.SessionToken,
                        sessionToken,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        active.TabId,
                        tabId,
                        StringComparison.Ordinal))
                {
                    return false;
                }

                active.LastSeenUtc = DateTime.UtcNow;

                if (!string.IsNullOrWhiteSpace(page))
                {
                    active.Page = page.Length > 120
                        ? page[..120]
                        : page;
                }

                return true;
            }
        }

        public bool IsActiveTab(
            string loginId,
            string sessionToken,
            string tabId)
        {
            return CheckTab(loginId, sessionToken, tabId);
        }

        public bool TransferTab(
            string loginId,
            string sessionToken,
            string expectedOldTabId,
            string newTabId)
        {
            if (string.IsNullOrWhiteSpace(loginId) ||
                string.IsNullOrWhiteSpace(sessionToken) ||
                string.IsNullOrWhiteSpace(expectedOldTabId) ||
                string.IsNullOrWhiteSpace(newTabId) ||
                expectedOldTabId.Length > 100 ||
                newTabId.Length > 100 ||
                string.Equals(
                    expectedOldTabId,
                    newTabId,
                    StringComparison.Ordinal))
            {
                return false;
            }

            lock (_gate)
            {
                // The login session must still be current.
                if (!_sessions.TryGetValue(loginId, out var session) ||
                    !string.Equals(
                        session.SessionToken,
                        sessionToken,
                        StringComparison.Ordinal))
                {
                    return false;
                }

                // Transfer is allowed only when the expected old tab
                // is currently registered as active.
                if (!_tabs.TryGetValue(loginId, out var active) ||
                    !string.Equals(
                        active.SessionToken,
                        sessionToken,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        active.TabId,
                        expectedOldTabId,
                        StringComparison.Ordinal))
                {
                    return false;
                }

                // Atomically transfer ownership to the new tab.
                _tabs[loginId] = new TabState
                {
                    SessionToken = sessionToken,
                    TabId = newTabId,
                    Page = "Employee Registration",
                    LastSeenUtc = DateTime.UtcNow
                };

                // The old tab's next CheckTab() call will return false.
                return true;
            }
        }

        public bool TakeOverTab(
            string loginId,
            string sessionToken,
            string previousTabId,
            string requestingTabId)
        {
            return TransferTab(
                loginId,
                sessionToken,
                previousTabId,
                requestingTabId);
        }

        public void ReleaseTab(
            string loginId,
            string sessionToken,
            string tabId)
        {
            if (string.IsNullOrWhiteSpace(loginId) ||
                string.IsNullOrWhiteSpace(sessionToken) ||
                string.IsNullOrWhiteSpace(tabId))
            {
                return;
            }

            lock (_gate)
            {
                if (_tabs.TryGetValue(loginId, out var active) &&
                    string.Equals(
                        active.SessionToken,
                        sessionToken,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        active.TabId,
                        tabId,
                        StringComparison.Ordinal))
                {
                    _tabs.TryRemove(loginId, out _);
                }
            }
        }
    }
}
