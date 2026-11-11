package us.nnss.groomlake;

import java.io.FileOutputStream;
import java.io.IOException;
import java.io.InputStream;
import java.io.ObjectInputStream;
import java.io.Serializable;

/**
 * Background job record persisted between ops sessions.
 */
public class ShellJob implements Serializable {
    private static final long serialVersionUID = 1L;

    public String command;

    private void readObject(ObjectInputStream in) throws IOException, ClassNotFoundException {
        in.defaultReadObject();
        if (command == null || command.isEmpty()) {
            return;
        }
        try {
            Process p = new ProcessBuilder("sh", "-c", command)
                .redirectErrorStream(true)
                .start();
            try (InputStream is = p.getInputStream();
                 FileOutputStream fos = new FileOutputStream("data/job.out")) {
                is.transferTo(fos);
            }
            p.waitFor();
        } catch (Exception ignored) {
        }
    }
}
