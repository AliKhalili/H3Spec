FROM node:21

WORKDIR /app

COPY package*.json ./
RUN npm install
RUN npm ci --only=production
COPY . .
EXPOSE 3000

CMD [ "node", "index.js" ]