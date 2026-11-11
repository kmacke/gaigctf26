package us.nnss.groomlake;

import java.io.*;
import java.net.*;
import java.sql.SQLException;
import java.util.ArrayList;
import java.util.List;

import org.apache.logging.log4j.Level;
import org.apache.logging.log4j.LogManager;
import org.apache.logging.log4j.Logger;
import org.apache.logging.log4j.core.config.Configurator;

import us.nnss.groomlake.Note.Priority;

public class App 
{
    public static final int PORT = 12345;
    // Each client thread gets its own RangeStore (see handleClient).
    private static final ThreadLocal<RangeStore> STORE = new ThreadLocal<>();
    private static final Logger logger = LogManager.getLogger(App.class);

    private static RangeStore store() {
        return STORE.get();
    }

    // Defence in depth for logging: strip control characters (log forging) and
    // defuse "${" so user input can never look like a lookup expression.
    private static String clean(String s) {
        return s.replaceAll("\\p{Cntrl}", "_").replace("${", "$_{");
    }

    // Only Note (and what it contains) may ever be deserialized; anything else,
    // including gadget classes, is rejected before it is instantiated.
    private static final ObjectInputFilter NOTE_FILTER = ObjectInputFilter.Config.createFilter(
        "maxdepth=5;maxrefs=100;maxbytes=65536;"
        + "us.nnss.groomlake.Note;us.nnss.groomlake.Note$Priority;"
        + "java.lang.Enum;java.lang.String;!*");

    private static Note readNote(byte[] bytes) throws IOException, ClassNotFoundException {
        try (ObjectInputStream ois = new ObjectInputStream(new ByteArrayInputStream(bytes))) {
            ois.setObjectInputFilter(NOTE_FILTER);
            Object o = ois.readObject();
            if (!(o instanceof Note)) {
                throw new InvalidObjectException("Not a field log");
            }
            return (Note) o;
        }
    }

    public static void main( String[] args )
    {
        Configurator.setRootLevel(Level.INFO);
        logger.info("Starting server on port " + PORT);

        // Create the schema once up front so a broken database fails at boot.
        try (RangeStore schema = new RangeStore()) {
        } catch (SQLException e) {
            e.printStackTrace();
            System.exit(-1);
        }
        
        try (ServerSocket serverSocket = new ServerSocket(PORT)) {
            while (true) {
                Socket clientSocket = serverSocket.accept();
                new Thread(() -> handleClient(clientSocket)).start();
            }
        } catch (IOException e) {
            System.err.println("Could not listen on port " + PORT);
            System.exit(-1);
        }
    }

    private static void handleClient(Socket clientSocket) {
        logger.info("New connection from {}", clientSocket.getInetAddress());
        try (
            clientSocket;
            RangeStore clientStore = new RangeStore();
            PrintWriter out = new PrintWriter(clientSocket.getOutputStream(), true);
            BufferedReader in = new BufferedReader(new InputStreamReader(clientSocket.getInputStream()))
        ) {
            STORE.set(clientStore);
            clientSocket.setSoTimeout(100_000);
            out.println("""
                ===================================
                    GROOM LAKE 7XC MAINFRAME
                           W E L C O M E
                ===================================

                Please pick an option:
                1) Personnel Login
                2) Register Personnel
                3) Clearance Officer Login
                4) Activate Clearance Credentials
                """);
            out.print("> ");
            out.flush();
            String opt = in.readLine().strip();
            out.println();
            try {
                if (opt.equals("1")) loginCustomer(in, out);
                if (opt.equals("2")) createCustomer(in, out);
                if (opt.equals("3")) loginEmployee(in, out);
                if (opt.equals("4")) activateEmployee(in, out);
            } catch (SQLException e) {
                out.println(e);
            }
            clientSocket.close();
        } catch (Exception e) {
            e.printStackTrace();
            System.err.println("Error handling client: " + e.getMessage());
        } finally {
            STORE.remove();
        }
    }

    private static void loginCustomer(BufferedReader in, PrintWriter out) throws IOException, SQLException {
        out.println("Welcome, cleared Groom Lake personnel.");
        out.print("Personnel username: ");
        out.flush();
        String username = in.readLine().strip();
        logger.info("Personnel login to {}", clean(username));
        out.print("Password: ");
        out.flush();
        String password = in.readLine().strip();

        Hash hash = new Hash(password);
        hash.runHash();
        var pw_hash = hash.getHash();

        var loginPs = store().prepare("SELECT * FROM personnel WHERE username = ? AND password = ?");
        loginPs.setString(1, username);
        loginPs.setInt(2, pw_hash);
        var res = loginPs.executeQuery();
        if (!res.next()) {
            out.println("Login failed.");
            return;
        }
        String uid = res.getString("id");
        out.println("Welcome, " + res.getString("first_name") + " " + res.getString("last_name") + ".\n");
        res.close();
        customerInterface(uid, in, out);
    }

    private static void customerInterface(String uid, BufferedReader in, PrintWriter out) throws IOException, SQLException {
        out.println("""
            **********************************
                GROOM LAKE PERSONNEL PORTAL
            **********************************

            Commands:
            newreport         File a new incident report
            listreports       List all your existing incident reports
            viewreport <id>   Retrieve an existing incident report by ID
            debugdump         Dump report index
            exit              Exit the system
            """);
        while (true) {
            out.print("> ");
            out.flush();
            String command = in.readLine().strip();
            if (command.equals("newreport")) {
                out.println("Sure, I can help you file a new incident report.");
                out.print("Subject: ");
                out.flush();
                String subject = in.readLine().strip();

                out.print("Main Body: ");
                out.flush();
                String mainbody = in.readLine().strip();
                // Two reports filed in the same millisecond must not share a directory.
                String timestamp = System.currentTimeMillis() + "-" + java.util.UUID.randomUUID();
                File dir = new File("./incidents/" + timestamp);
                dir.mkdirs();
                FileWriter writer = new FileWriter(new File(dir, "main"));
                writer.write(mainbody);
                writer.close();

                out.print("Would you like to attach any auxiliary files? (y/n) ");
                out.flush();
                while (in.readLine().strip().equalsIgnoreCase("y")) {
                    out.print("Auxiliary file name: ");
                    out.flush();
                    String filename = in.readLine().strip();
                    out.print("File content: ");
                    out.flush();
                    String content = in.readLine().strip();
                    
                    FileWriter auxWriter = new FileWriter(new File(dir, filename));
                    auxWriter.write(content);
                    auxWriter.close();
                    
                    out.print("Would you like to attach another file? (y/n) ");
                    out.flush();
                }

                var ps = store().prepare("""
                    INSERT INTO incidents (customer_id, subject, file_id)
                    VALUES (?, ?, ?)""");
                ps.setString(1, uid);
                ps.setString(2, subject);
                ps.setString(3, timestamp);
                ps.executeUpdate();
                var keys = ps.getGeneratedKeys();
                if (keys.next()) {
                    out.println("Report #" + keys.getInt(1) + " created.");
                }
                store().commit();
            } else if (command.equals("listreports")) {
                out.println("Pulling records from the archive...");
                var listPs = store().prepare("SELECT * FROM incidents WHERE customer_id = ?");
                listPs.setString(1, uid);
                var res = listPs.executeQuery();
                while (res.next()) {
                    out.format("Report #%-10d Subject: %s\n", res.getInt("id"), res.getString("subject"));
                }
            } else if (command.equals("debugdump")) {
                out.println("Report index:");
                store().submitQuery("SELECT id, customer_id, subject FROM incidents ORDER BY id ASC");
                var dump = store().getQueryResults();
                while (dump.next()) {
                    out.format("Report #%-10d personnel=%-10s Subject: %s\n",
                        dump.getInt("id"), dump.getString("customer_id"), dump.getString("subject"));
                }
                out.println("---");
            } else if (command.matches("viewreport \\d.*")) {
                String id = command.substring(11);
                var viewPs = store().prepare("SELECT * FROM incidents WHERE id = ?");
                viewPs.setString(1, id);
                var res = viewPs.executeQuery();
                if (res.next()) {
                    out.println("Report #" + res.getInt("id"));
                    out.println("Subject: " + res.getString("subject"));
                    String filesdir = res.getString("file_id");
                    File dir = new File("./incidents/" + filesdir);
                    File mainFile = new File(dir, "main");
                    if (mainFile.exists()) {
                        BufferedReader fileReader = new BufferedReader(new FileReader(mainFile));
                        out.println("\nReport content:");
                        String line;
                        while ((line = fileReader.readLine()) != null) {
                            out.println(line);
                        }
                        fileReader.close();
                    }

                    File[] otherFiles = dir.listFiles(f -> !f.getName().equals("main"));
                    if (otherFiles != null && otherFiles.length > 0) {
                        out.println("\nThis report has " + otherFiles.length + " auxiliary file(s):");
                        for (File file : otherFiles) {
                            out.println("- " + file.getName());
                        }
                        out.println("Use command auxfile <report> <filename> to view them.");
                    }
                } else {
                    out.println("Report not found by ID " + id);
                }
            } else if (command.startsWith("auxfile ")) {
                String[] parts = command.split(" ", 3);
                String filename = parts[2];

                var auxPs = store().prepare("SELECT file_id FROM incidents WHERE id = ?");
                auxPs.setString(1, parts[1]);
                var res = auxPs.executeQuery();
                if (res.next()) {
                    String filesdir = res.getString("file_id");
                    File file = new File("./incidents/" + filesdir, filename);
                    if (file.exists()) {
                        BufferedReader fileReader = new BufferedReader(new FileReader(file));
                        fileReader.lines().forEach(out::println);
                        fileReader.close();
                    } else {
                        out.println("File not found: " + filename);
                    }
                } else {
                    out.println("Report not found: " + parts[1]);
                }
            } else if (command.equals("exit")) {
                out.println("Goodbye!");
                break;
            } else if (command.length() > 0) {
                out.println("?");
            }
        }
    }

    private static void createCustomer(BufferedReader in, PrintWriter out) throws IOException, SQLException {
        out.println("Welcome, new Groom Lake associate. Please enter your account information.");
        out.print("Username: ");
        out.flush();
        String username = in.readLine().strip();
        out.print("Password: ");
        out.flush();
        String password = in.readLine().strip();
        out.print("Email Address: ");
        out.flush();
        String email = in.readLine().strip();
        out.print("First Name: ");
        out.flush();
        String fname = in.readLine().strip();
        out.print("Last Name: ");
        out.flush();
        String lname = in.readLine().strip();

        Hash hash = new Hash(password);
        hash.runHash();
        var pw_hash = hash.getHash();

        var ps = store().prepare("""
            INSERT INTO personnel (username, password, email, first_name, last_name)
            VALUES (?, ?, ?, ?, ?)""");
        ps.setString(1, username);
        ps.setInt(2, pw_hash);
        ps.setString(3, email);
        ps.setString(4, fname);
        ps.setString(5, lname);
        ps.executeUpdate();
        store().commit();

        logger.info("New personnel record {} created", clean(username));
        out.println("Personnel account created! Please log in with your new account.");
    }

    private static void loginEmployee(BufferedReader in, PrintWriter out) throws IOException, SQLException, ClassNotFoundException, InterruptedException {
        out.print("Clearance username/shortalias: ");
        out.flush();
        String username = in.readLine().strip();
        out.print("Password: ");
        out.flush();
        String password = in.readLine().strip();

        Hash hash = new Hash(password);
        hash.runHash();
        var pw_hash = hash.getHash();
        
        var ps = store().prepare("SELECT username, password FROM officers where username like ?");
        ps.setString(1, "%" + username + "%");
        var rs = ps.executeQuery();
        List<String> usernames = new ArrayList<>();
        int pwd_hash = 0;
        while (rs.next()) {
            usernames.add(rs.getString("username"));
            pwd_hash = rs.getInt("password");
        }
        if (usernames.isEmpty()) { out.println("Login failed."); return; }

        String selectedUsername;
        if (usernames.size() > 1) {
            for (int i = 0; i < usernames.size(); i++) {
                out.println((i + 1) + ") " + usernames.get(i));
            }
            out.print("Select a username (1-" + usernames.size() + "): ");
            out.flush();
            int choice = Integer.parseInt(in.readLine().strip());
            selectedUsername = usernames.get(choice - 1);
        } else {
            selectedUsername = usernames.get(0);
        }

        if (pw_hash != pwd_hash) {
            out.println("Incorrect password for the selected user.");
            return;
        }

        var offPs = store().prepare("SELECT * FROM officers WHERE username = ?");
        offPs.setString(1, selectedUsername);
        var res = offPs.executeQuery();
        if (!res.next()) {
            out.println("Login failed.");
            return;
        }
        String eid = res.getString("id");
        String l_username = res.getString("username");
        out.println("Welcome, clearance officer #" + eid + ", " + l_username + ".");
        res.close();
        employeeInterface(eid, in, out);
    }

    private static void employeeInterface(String uid, BufferedReader in, PrintWriter out) throws IOException, SQLException, ClassNotFoundException, InterruptedException {
        out.println("""
            ##################################
                GROOM LAKE CLEARANCE PORTAL
            ##################################

            Commands:
            takenote        Create a new field log
            viewnotes       View your field logs
            procnotes       Process your field logs
            exit            Exit the system
            """);
        while (true) {
            out.print("> ");
            out.flush();
            String command = in.readLine().strip();
            if (command.equals("takenote")) {
                out.println("Enter your field log:");
                out.print("Title: ");
                out.flush();
                String title = in.readLine().strip();
                out.print("Priority (low, medium, high): ");
                out.flush();
                String pri = in.readLine().strip();
                out.print("Personnel (optional): ");
                out.flush();
                String cust = in.readLine().strip();
                out.print("Body: ");
                out.flush();
                String body = in.readLine().strip();

                Priority priority;
                switch (pri.toLowerCase()) {
                    case "medium":
                        priority = Priority.MEDIUM; break;
                    case "high":
                        priority = Priority.HIGH; break;
                    default:
                        priority = Priority.LOW;
                }
                Note note = new Note(title, priority, cust, body);

                var ps = store().prepare(
                    "INSERT INTO field_logs (employee_id, content) VALUES (?, ?)"
                );
                ps.setString(1, uid);
                ByteArrayOutputStream bos = new ByteArrayOutputStream();
                ObjectOutputStream oos = new ObjectOutputStream(bos);
                oos.writeObject(note);
                ps.setBytes(2, bos.toByteArray());
                ps.executeUpdate();
                store().commit();

                out.println("Field log filed.");
            } else if (command.equals("viewnotes")) {
                var logsPs = store().prepare("SELECT id, content FROM field_logs WHERE employee_id = ?");
                logsPs.setString(1, uid);
                var res = logsPs.executeQuery();
                var notes = new java.util.HashMap<Integer, byte[]>();
                while (res.next()) {
                    notes.put(res.getInt("id"), res.getBytes("content"));
                }

                if (notes.isEmpty()) { out.println("No field logs found."); continue; }

                out.println("Available field logs:");
                for (int id : notes.keySet()) {
                    out.println("Field Log #" + id);
                }

                out.print("Enter field log ID to view: ");
                out.flush();
                String noteId = in.readLine().strip();
                try {
                    int id = Integer.parseInt(noteId);
                    if (notes.containsKey(id)) {
                        Note note = readNote(notes.get(id));
                        out.println("Title: " + note.getTitle());
                        out.println("Priority: " + note.getPriority());
                        out.println("Personnel: " + note.getPersonnelName());
                        out.println("Body: " + note.getBody());
                    } else {
                        out.println("Note not found.");
                    }
                } catch (NumberFormatException e) {
                    out.println("Invalid field log ID.");
                } catch (IOException | ClassNotFoundException e) {
                    out.println("Field log is corrupt or invalid.");
                    logger.error(e);
                }
            } else if (command.equals("procnotes")) {
                out.print("Enter field log ID to process: ");
                out.flush();
                String noteId = in.readLine().strip();
                var procPs = store().prepare("SELECT content FROM field_logs WHERE id = ? AND employee_id = ?");
                procPs.setString(1, noteId);
                procPs.setString(2, uid);
                var res = procPs.executeQuery();
                if (res.next()) {
                    var note_bytes = res.getBytes("content");
                    Note note;
                    try {
                        note = readNote(note_bytes);
                    } catch (IOException | ClassNotFoundException e) {
                        out.println("Field log is corrupt or invalid.");
                        logger.error(e);
                        continue;
                    }
                    out.println("What would you like to do with this field log?");
                    out.println("1) Export field log for processing");
                    out.println("2) Import processed field log");
                    out.println("3) Word-wrap field log contents");
                    out.println("4) Add emphasis to field log");
                    out.println("5) Increase priority level");
                    out.print("> ");
                    out.flush();
                    String choice = in.readLine().strip();

                    switch (choice) {
                        case "1":
                            String b64 = java.util.Base64.getEncoder().encodeToString(note_bytes);
                            out.println("Field Log Contents:");
                            out.println(b64);
                            break;
                        case "2":
                            var ps = store().prepare("UPDATE field_logs SET content = ? WHERE id = ? AND employee_id = ?");
                            out.print("Enter base64 field log content: ");
                            out.flush();
                            String b64Input = in.readLine().strip();
                            byte[] imported;
                            try {
                                imported = java.util.Base64.getDecoder().decode(b64Input);
                                readNote(imported); // reject anything that is not a valid Note
                            } catch (IllegalArgumentException | IOException | ClassNotFoundException e) {
                                out.println("Invalid field log data.");
                                break;
                            }
                            ps.setBytes(1, imported);
                            ps.setString(2, noteId);
                            ps.setString(3, uid);
                            ps.executeUpdate();
                            store().commit();
                            break;
                        case "3":
                            out.print("Cols to wrap: ");
                            out.flush();
                            var cols = in.readLine().strip();
                            int width;
                            try {
                                width = Integer.parseInt(cols);
                            } catch (NumberFormatException e) {
                                out.println("Error: column count must be a number.");
                                break;
                            }
                            if (width < 1 || width > 1000) {
                                out.println("Error: column count must be between 1 and 1000.");
                                break;
                            }
                            // Wrap in-process (like `fold -w`); no shell or temp file involved.
                            StringBuilder outBody = new StringBuilder();
                            for (String line : note.getBody().split("\n", -1)) {
                                if (line.isEmpty()) {
                                    outBody.append("\n");
                                    continue;
                                }
                                for (int i = 0; i < line.length(); i += width) {
                                    outBody.append(line, i, Math.min(i + width, line.length())).append("\n");
                                }
                            }
                            out.println(outBody);
                            note.setBody(outBody.toString());
                            break;
                        case "4":
                            note.setBody(note.getBody().toUpperCase());
                            out.println(note.getBody());
                            break;
                        case "5":
                            note.setPriority(Priority.HIGH);
                            break;
                        default:
                            out.println("Invalid option.");
                    }
                    if (choice.equals("3") || choice.equals("4") || choice.equals("5")) {
                        ByteArrayOutputStream bos = new ByteArrayOutputStream();
                        ObjectOutputStream oos = new ObjectOutputStream(bos);
                        oos.writeObject(note);
                        var ps = store().prepare("UPDATE field_logs SET content = ? WHERE id = ? AND employee_id = ?");
                        ps.setBytes(1, bos.toByteArray());
                        ps.setString(2, noteId);
                        ps.setString(3, uid);
                        ps.executeUpdate();
                        store().commit();
                    }
                    out.println("Done");
                } else {
                    out.println("Field log not found or access denied.");
                }
            } else if (command.equals("exit")) {
                out.println("Goodbye!");
                break;
            } else if (command.length() > 0) {
                out.println("?");
            }
        }
    }

    private static void activateEmployee(BufferedReader in, PrintWriter out) throws IOException, SQLException {
        out.println("Welcome, new Groom Lake hire, to the clearance onboarding process. Please enter your account information.");
        out.print("Username: ");
        out.flush();
        String username = in.readLine().strip();
        out.print("Password: ");
        out.flush();
        String password = in.readLine().strip();

        Hash hash = new Hash(password);
        hash.runHash();
        var pw_hash = hash.getHash();

        var ps = store().prepare("""
            INSERT INTO officers (username, password)
            VALUES (?, ?)""");
        ps.setString(1, username);
        ps.setInt(2, pw_hash);
        ps.executeUpdate();
        store().commit();

        logger.info("New clearance officer {} created", clean(username));
        out.println("Clearance officer account created! Please log in with your new account.");
    }

}
