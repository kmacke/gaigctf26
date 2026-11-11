package us.nnss.groomlake;

public class Hash {
    
    private String orig;
    private int hash;

    public Hash(String orig) {
        this.orig = orig;
    }

    public void runHash() {
        int sum = 50;
        for (char c : this.orig.toCharArray()) {
            sum += (int) c;
        }
        this.hash = sum;
        this.hash *= 2;
        this.hash -= 99;
    }

    public int getHash() {
        return hash;
    }

}
