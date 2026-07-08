-- Sangam Credit Cooperative — dogfood II. Specimens S1–S7 (see README).

-- S5: DOMAIN carrying a reusable constraint. Introspection must chase it.
CREATE DOMAIN share_amount AS NUMERIC(10,2) CHECK (VALUE >= 100);

CREATE TABLE members (
  member_id     SERIAL PRIMARY KEY,
  full_name     VARCHAR(120) NOT NULL,
  phone         VARCHAR(10) NOT NULL CHECK (length(phone) = 10),      -- Opaque (fn)
  age           INT NOT NULL,
  guardian_member_id INT NULL REFERENCES members(member_id),
  share_balance share_amount NOT NULL,                                 -- S5 site 1
  "riskGrade"   VARCHAR(2) NOT NULL
    CONSTRAINT risk_grade_window CHECK ("riskGrade" >= 'A' AND "riskGrade" <= 'E'),  -- S6
  -- S2: OR eligibility — minors admitted with a guardian
  CONSTRAINT member_eligibility CHECK (age >= 21 OR guardian_member_id IS NOT NULL)
);

CREATE TABLE deposits (
  deposit_id   SERIAL PRIMARY KEY,
  member_id    INT NOT NULL REFERENCES members(member_id),
  amount       share_amount NOT NULL,                                  -- S5 site 2
  opened_on    DATE NOT NULL DEFAULT CURRENT_DATE,
  maturity_date DATE NOT NULL,
  rate_pct     NUMERIC(4,2) NOT NULL
    CONSTRAINT deposit_rate_window CHECK (rate_pct >= 3.5 AND rate_pct <= 9.25),
  CONSTRAINT deposit_min CHECK (amount >= 500),
  CONSTRAINT deposit_maturity_after_open CHECK (maturity_date > opened_on)  -- Opaque (cross-col)
);

CREATE TABLE loans (
  loan_id      SERIAL PRIMARY KEY,
  member_id    INT NOT NULL REFERENCES members(member_id),
  principal    NUMERIC(12,2) NOT NULL
    CONSTRAINT loan_principal_window CHECK (principal >= 1000 AND principal <= 500000),
  tenure_months INT NOT NULL CHECK (tenure_months BETWEEN 3 AND 84),   -- Opaque (BETWEEN, canary 2)
  interest_pct NUMERIC(4,2) NOT NULL CHECK (interest_pct > 0),
  -- S4: subsumption chain — the original cap and the tightening amendment
  CONSTRAINT loan_interest_cap_2019 CHECK (interest_pct <= 24),
  CONSTRAINT loan_interest_cap_2024 CHECK (interest_pct <= 18)
);

CREATE TABLE guarantees (
  loan_id             INT NOT NULL REFERENCES loans(loan_id),
  guarantor_id        INT NULL REFERENCES members(member_id),
  guarantor_share_pct NUMERIC(4,1) NULL,
  CONSTRAINT guarantor_share_floor CHECK (guarantor_share_pct >= 10),
  PRIMARY KEY (loan_id, guarantor_id)
);

CREATE TABLE ledger (
  entry_id    SERIAL PRIMARY KEY,
  member_id   INT NOT NULL REFERENCES members(member_id),
  entry_on    TIMESTAMPTZ NOT NULL DEFAULT now(),
  -- S3: negative bounds — canary specimen for the signed-number parser gap
  ledger_adjustment NUMERIC(8,2) NOT NULL
    CONSTRAINT adjustment_window CHECK (ledger_adjustment >= -5000 AND ledger_adjustment <= 5000),
  method      VARCHAR(10) NOT NULL CHECK (method IN ('cash','neft','upi'))  -- Opaque (IN)
);
