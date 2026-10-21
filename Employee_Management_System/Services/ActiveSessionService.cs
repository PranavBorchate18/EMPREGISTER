
using Employee_Management_System.Models;
using System;
using System.Collections.Concurrent;

namespace Employee_Management_System.Services
{
    // Manages active login sessions and browser tabs.
    // This implementation is for a single application instance.
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

        // Get the current login session.
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

        // Check whether the supplied login token is still current.
        public bool IsCurrent(string loginId, string token)
        {
            if (string.IsNullOrWhiteSpace(loginId) ||
                string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            lock (_gate)
            {
                return _sessions.TryGetValue(loginId, out var session)
                    && string.Equals(
                        session.SessionToken,
                        token,
                        StringComparison.Ordinal);
            }
        }

        // Start a session. Replacing an existing session requires
        // its exact current token.
        public bool TryStart(
            ActiveLoginSession next,
            string? expectedOldToken,
            out ActiveLoginSession? conflict)
        {
            if (next == null ||
                string.IsNullOrWhiteSpace(next.LoginId) ||
                string.IsNullOrWhiteSpace(next.SessionToken))
            {
                conflict = null;
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
                _tabs.TryRemove(next.LoginId, out _);

                conflict = null;
                return true;
            }
        }

        // Remove a session only if its token is still current.
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

        // Return information about the currently active tab.
        public TabDetails? GetActiveTab(
            string loginId,
            string sessionToken)
        {
            lock (_gate)
            {
                if (!IsCurrent(loginId, sessionToken) ||
                    !_tabs.TryGetValue(loginId, out var tab) ||
                    tab.SessionToken != sessionToken)
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

        // Register the first active tab for this login.
        public bool TryClaimTab(
            string loginId,
            string sessionToken,
            string tabId)
        {
            if (string.IsNullOrWhiteSpace(tabId) || tabId.Length > 100)
                return false;

            lock (_gate)
            {
                if (!IsCurrent(loginId, sessionToken))
                    return false;

                if (_tabs.TryGetValue(loginId, out var existing))
                {
                    if (existing.SessionToken != sessionToken ||
                        existing.TabId != tabId)
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

        // Verify that a request comes from the active tab.
        public bool CheckTab(
            string loginId,
            string sessionToken,
            string tabId,
            string? page = null)
        {
            lock (_gate)
            {
                if (!IsCurrent(loginId, sessionToken) ||
                    !_tabs.TryGetValue(loginId, out var active) ||
                    active.SessionToken != sessionToken ||
                    active.TabId != tabId)
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

        // Transfer tab ownership only if the expected old tab
        // still owns the active tab claim.
        // The old tab will fail CheckTab after a successful transfer.
        public bool TransferTab(
            string loginId,
            string sessionToken,
            string expectedOldTabId,
            string newTabId)
        {
            if (string.IsNullOrWhiteSpace(expectedOldTabId) ||
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
                if (!IsCurrent(loginId, sessionToken) ||
                    !_tabs.TryGetValue(loginId, out var active) ||
                    active.SessionToken != sessionToken ||
                    active.TabId != expectedOldTabId)
                {
                    return false;
                }

                _tabs[loginId] = new TabState
                {
                    SessionToken = sessionToken,
                    TabId = newTabId,
                    Page = active.Page,
                    LastSeenUtc = DateTime.UtcNow
                };

                return true;
            }
        }

        // Explicitly named helper for the Yes Continue action.
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

        // Release a tab only when it still owns the active claim.
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
                    active.SessionToken == sessionToken &&
                    active.TabId == tabId)
                {
                    _tabs.TryRemove(loginId, out _);
                }
            }
        }
    }
}
