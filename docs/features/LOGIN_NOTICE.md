# Login notice — a player who logs in during a running event is told what is running

**Status:** in progress for 0.13.1 (docs/dod/login-notice.md, audit docs/audits/login-notice.md; design §9 D63, Epic
A45). Step 1 (the text, the delivery rule, the switch and its checks) is built; the in-game session and the release
follow.

## Goal

A player who connects while an event is running (started and not yet ended, not merely enabled) gets one private
chat line about 10 s later naming the running events and their time left, so nobody walks into a siege or an
apocalypse unaware. It is on by default and turned off with one cfg switch.

## Design summary

- **The line** (Logic/Messages.LoginNotice): "Events in progress: &lt;part&gt;[; &lt;part&gt; …]. Type .nyar status
  for details." Each part is exactly the line `.nyar status` shows for that event ("Bandit raid: 5 min left", or an
  apocalypse's own status line), in the same end-time order. A line that would pass Wire.MaxBytes (480) leaves whole
  parts out and ends "… and &lt;m&gt; more". Nothing running: no line.
- **Who and when** (Logic/LoginNotices, driven by Services/Announcer): every user who connects, admin or not, while
  `[Announcements] LoginEventNotice` is on is queued; the notice leaves 10 s after the connect, reading the running
  list then. Nothing is sent if nothing runs at that moment, if the player left, or if another player now holds the user
  entity. A player who was sent a notice gets none for a connect within 60 s of the previous one; a connect that sent
  nothing does not use the 60 s window up. The admin degraded notice keeps its own gate and still arrives.
- **The switch:** `[Announcements] LoginEventNotice`, default `true`, the one announcement switch on by default
  (design §9 D63, the exception to S-24); preflight's announcement check requires that default and `false` for every
  other switch.
- **Failures:** a send that throws is logged once per streak as "[nyar] private message failed: …" (the streak the
  degraded notice shares), and the other due notices still leave; a running-list read that throws sends nothing and
  logs "[nyar] login notice failed: …" once per streak.
- **Logs and privacy:** "[nyar] login notice sent: &lt;k&gt; events" names no player, id or position. The switch is not
  on the wire (Raphael api 8 is unchanged).

## Open questions

- On a slow-loading remote server a line sent 10 s after the connect may reach a client still on its loading screen
  and be missed. The G-Portal test weekend confirms whether 10 s is enough there (S-3).

## Test results

