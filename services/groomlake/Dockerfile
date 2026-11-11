FROM maven:3.9.9-eclipse-temurin-21@sha256:3a4ab3276a087bf276f79cae96b1af04f53731bec53fb2e651aca79e4b10211e AS build
WORKDIR /app
COPY pom.xml .
RUN mvn -q dependency:go-offline
COPY src ./src
RUN mvn -q compile dependency:copy-dependencies -DincludeScope=runtime

# Runs offline: everything Maven would fetch is resolved at build time.
FROM eclipse-temurin:21-jre@sha256:0b99b3376fbb50dbf9c0af8535fdd5b8eb0d3fdfc8b1e4967f383b6919f6901a
WORKDIR /app
COPY --from=build /app/target/classes ./classes
COPY --from=build /app/target/dependency ./lib
RUN mkdir -p data incidents
EXPOSE 12345
ENTRYPOINT ["java", "-cp", "classes:lib/*", "us.nnss.groomlake.App"]
