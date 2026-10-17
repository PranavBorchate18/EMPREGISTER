
using Employee_Management_System.Models;
using System;
using System.Collections.Concurrent;

namespace Employee_Management_System.Services
{
    public class ActiveSessionService
    {
        // ==========================================
        // SINGLE ACTIVE LOGIN SESSION
        // ==========================================

        private readonly ConcurrentDictionary<string, ActiveLoginSession>
            _sessions = new(StringComparer.OrdinalIgnoreCase);

        private readonly object _gate = new();

        public ActiveLoginSession? Get(string loginId)
        {
            lock (_gate)
            {
                return _sessions.TryGetValue(loginId, out var session)
                    ? session
                    : null;
            }
        }

        public bool IsCurrent(string loginId, string token)
        {
            lock (_gate)
            {
                return _sessions.TryGetValue(loginId, out var session)
                    && string.Equals(
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

                // A new login invalidates any old tab ownership.
                _activeTabs.TryRemove(next.LoginId, out _);

                conflict = null;
                return true;
            }
        }

        public void RemoveIfCurrent(string loginId, string token)
        {
            lock (_gate)
            {
                if (_sessions.TryGetValue(loginId, out var existing)
                    && string.Equals(
                        existing.SessionToken,
                        token,
                        StringComparison.Ordinal))
                {
                    _sessions.TryRemove(loginId, out _);
                    _activeTabs.TryRemove(loginId, out _);
                }
            }
        }

        // ==========================================
        // SINGLE ACTIVE TAB
        // ==========================================

        private sealed class ActiveTabInfo
        {
            public string SessionToken { get; set; } = "";
            public string TabId { get; set; } = "";
            public DateTime LastSeenUtc { get; set; }
        }

        private readonly ConcurrentDictionary<string, ActiveTabInfo>
            _activeTabs = new(StringComparer.OrdinalIgnoreCase);

        // Check whether a browser tab is active.
        public bool IsActiveTab(
            string loginId,
            string sessionToken,
            string tabId)
        {
            if (string.IsNullOrWhiteSpace(loginId) ||
                string.IsNullOrWhiteSpace(sessionToken) ||
                string.IsNullOrWhiteSpace(tabId))
            {
                return false;
            }

            lock (_gate)
            {
                if (!IsCurrent(loginId, sessionToken))
                    return false;

                return _activeTabs.TryGetValue(loginId, out var active)
                    && active.SessionToken == sessionToken
                    && active.TabId == tabId;
            }
        }

        // First tab claims ownership.
        // Other tabs cannot replace it automatically.
        public bool TryClaimTab(
            string loginId,
            string sessionToken,
            string tabId)
        {
            if (string.IsNullOrWhiteSpace(loginId) ||
                string.IsNullOrWhiteSpace(sessionToken) ||
                string.IsNullOrWhiteSpace(tabId))
            {
                return false;
            }

            lock (_gate)
            {
                if (!IsCurrent(loginId, sessionToken))
                    return false;

                if (_activeTabs.TryGetValue(loginId, out var active))
                {
                    if (active.SessionToken != sessionToken)
                        return false;

                    if (active.TabId != tabId)
                        return false;

                    active.LastSeenUtc = DateTime.UtcNow;
                    return true;
                }

                _activeTabs[loginId] = new ActiveTabInfo
                {
                    SessionToken = sessionToken,
                    TabId = tabId,
                    LastSeenUtc = DateTime.UtcNow
                };

                return true;
            }
        }

        // Refresh the active tab heartbeat.
        public bool CheckTab(
            string loginId,
            string sessionToken,
            string tabId)
        {
            lock (_gate)
            {
                if (!IsActiveTab(loginId, sessionToken, tabId))
                    return false;

                _activeTabs[loginId].LastSeenUtc = DateTime.UtcNow;
                return true;
            }
        }

        // Release ownership only for the matching tab.
        public void ReleaseTab(
            string loginId,
            string sessionToken,
            string tabId)
        {
            lock (_gate)
            {
                if (IsActiveTab(loginId, sessionToken, tabId))
                {
                    _activeTabs.TryRemove(loginId, out _);
                }
            }
        }
    }
}
