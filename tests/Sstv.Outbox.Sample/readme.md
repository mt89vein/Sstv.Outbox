CREATE TABLE my_outbox_items (
    id uuid NOT NULL,
    created_at timestamptz DEFAULT (now() at time zone 'utc') NOT NULL,
    status int4 default 0 NOT NULL,
    retry_count int4 NULL,
    retry_after timestamptz NULL,
    headers bytea NULL,
    data bytea NULL,
    CONSTRAINT pk_my_outbox_items PRIMARY KEY (id)
);

CREATE TABLE one_more_outbox_items (
    id uuid NOT NULL,
    created_at timestamptz DEFAULT (now() at time zone 'utc') NOT NULL,
    status int4 default 0 NOT NULL,
    retry_count int4 NULL,
    retry_after timestamptz NULL,
    headers bytea NULL,
    data bytea NULL,
    CONSTRAINT pk_one_more_outbox_items PRIMARY KEY (id)
);

CREATE TABLE strict_outbox_items (
    id uuid NOT NULL,
    created_at timestamptz DEFAULT (now() at time zone 'utc') NOT NULL,
    status int4 default 0 NOT NULL,
    retry_count int4 NULL,
    retry_after timestamptz NULL,
    headers bytea NULL,
    data bytea NULL,
    CONSTRAINT pk_strict_outbox_items PRIMARY KEY (id)
);