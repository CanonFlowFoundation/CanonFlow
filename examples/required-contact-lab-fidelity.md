# Required Contact Laboratory Fidelity Report

- Status: Experimental
- Claim: ConstructivelyProjected
- Scope: `CHECK (email IS NOT NULL OR phone IS NOT NULL)`
- Source digest: `sha256:8a71fd4510146dbd2bf2822eef5b7934bfef70612b3fa1ad97d69d5938c2bded`
- Predicate digest: `sha256:219eb47d66f888c4ba5d793e0c64a653ac484d80505c0239833c2d6d10926d60`
- Manifest protected digest: `sha256:ce387a386ef75ca1ae2e6b81b8071efd13943ffdf57423d06ab4159e806b8ec0`
- Admitted states: EmailOnly, PhoneOnly, Both
- Excluded state: both fields absent
- Oracle: four-row PostgreSQL truth table
- Limits: row-local laboratory pattern only; no regulatory authority
