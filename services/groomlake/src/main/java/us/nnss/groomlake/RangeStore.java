package us.nnss.groomlake;

import java.sql.*;
import java.util.ArrayList;
import java.util.List;

// One RangeStore per client connection; a JDBC connection and its last
// ResultSet must never be shared between threads.
public class RangeStore implements AutoCloseable {
    public final Connection conn;
    public ResultSet stmtResults;
    private final List<Statement> openStatements = new ArrayList<>();

    public RangeStore() throws SQLException {
        this.conn = DriverManager.getConnection("jdbc:sqlite:./data/data.db");
        try (Statement s = conn.createStatement()) {
            // Many clients write concurrently: WAL lets readers and a writer overlap,
            // and the busy timeout queues writers instead of failing them.
            s.execute("PRAGMA busy_timeout = 5000");
            s.execute("PRAGMA journal_mode = WAL");

            s.executeUpdate("""
                    CREATE TABLE IF NOT EXISTS personnel (
                        id INTEGER PRIMARY KEY AUTOINCREMENT,
                        username TEXT NOT NULL UNIQUE,
                        password INTEGER,
                        email TEXT,
                        first_name TEXT,
                        last_name TEXT,
                        created_at TEXT DEFAULT (datetime('now'))
                    )""");

            s.executeUpdate("""
                    CREATE TABLE IF NOT EXISTS incidents (
                        id INTEGER PRIMARY KEY AUTOINCREMENT,
                        customer_id INTEGER,
                        subject TEXT,
                        file_id TEXT,
                        created_at TEXT DEFAULT (datetime('now'))
                    )""");

            s.executeUpdate("""
                    CREATE TABLE IF NOT EXISTS officers (
                        id INTEGER PRIMARY KEY AUTOINCREMENT,
                        username TEXT NOT NULL UNIQUE,
                        password INTEGER,
                        created_at TEXT DEFAULT (datetime('now'))
                    )""");

            s.executeUpdate("""
                    CREATE TABLE IF NOT EXISTS field_logs (
                        id INTEGER PRIMARY KEY AUTOINCREMENT,
                        employee_id INTEGER,
                        content BLOB,
                        created_at TEXT DEFAULT (datetime('now'))
                    )""");
        } catch (SQLException e) {
            conn.close();
            throw e;
        }
    }

    public void submitQuery(String query) throws SQLException {
        closeOpenStatements();
        Statement s = conn.createStatement();
        openStatements.add(s);
        stmtResults = s.executeQuery(query);
    }

    // Every statement this connection handed out is closed before the next one
    // runs: an unfinished read (a query or a generated-keys lookup) pins an old
    // snapshot, and SQLite refuses to write from it once another client commits.
    public PreparedStatement prepare(String sql) throws SQLException {
        closeOpenStatements();
        PreparedStatement ps = conn.prepareStatement(sql);
        openStatements.add(ps);
        return ps;
    }

    private void closeOpenStatements() throws SQLException {
        for (Statement s : openStatements) {
            s.close();
        }
        openStatements.clear();
    }

    public ResultSet getQueryResults() {
        return stmtResults;
    }

    // Autocommit is on, so each statement is already durable; kept for callers.
    public void commit() throws SQLException {
    }

    @Override
    public void close() throws SQLException {
        conn.close();
    }
}
