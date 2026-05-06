FROM ubuntu:22.04

RUN apt-get update && apt-get install -y \
    libglib2.0-0 \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY . .
RUN chmod +x ./Survive.x86_64

EXPOSE 10000

CMD ["./Survive.x86_64", "-batchmode", "-nographics"]
