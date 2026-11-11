package us.nnss.groomlake;

public class Note implements java.io.Serializable {
    private String title;
    private Priority priority;
    private String personnelName;
    private String body;

    public enum Priority {
        LOW, MEDIUM, HIGH
    }

    public Note(String title, Priority priority, String body) {
        this.title = title;
        this.priority = priority;
        this.body = body;
    }

    public Note(String title, Priority priority, String personnelName, String body) {
        this(title, priority, body);
        this.personnelName = personnelName;
    }

    public String getTitle() { return title; }
    public Priority getPriority() { return priority; }
    public String getPersonnelName() { return personnelName; }
    public String getBody() { return body; }

    public void setTitle(String title) { this.title = title; }
    public void setPriority(Priority priority) { this.priority = priority; }
    public void setPersonnelName(String personnelName) { this.personnelName = personnelName; }
    public void setBody(String body) { this.body = body; }
}
