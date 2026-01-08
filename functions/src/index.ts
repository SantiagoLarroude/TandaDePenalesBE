import { setGlobalOptions } from "firebase-functions/v2";
import { onRequest } from "firebase-functions/v2/https";
import * as logger from "firebase-functions/logger";
import * as admin from "firebase-admin";
import { FieldValue } from "firebase-admin/firestore";
import cors from "cors";
const corsHandler = cors({ origin: true });

setGlobalOptions({ maxInstances: 10 });

if (!admin.apps.length) {
  admin.initializeApp();
}

enum ShootoutStatus {
  IN_PROGRESS = "IN_PROGRESS",
  FINISHED = "FINISHED"
}


export const getUser = onRequest((req, res) => 
  corsHandler(req, res as any, async () => {
    try {
      const userId = req.query.userId as string;

      if (!userId) {
        res.status(400).json({ error: "Missing userId" });
        return;
      }

      const userDoc = await admin
        .firestore()
        .collection("users")
        .doc(userId)
        .get();

      if (!userDoc.exists) {
        res.status(404).json({ error: "User not found" });
        return;
      }

      logger.info("User fetched", { userId });

      res.json({
        id: userDoc.id,
        ...userDoc.data(),
      });
    } catch (error) {
      logger.error("Error fetching user", error);
      res.status(500).json({ error: "Internal server error" });
    }
  })
);


export const startShootout = onRequest((req, res) =>
  corsHandler(req, res as any, async () => {
    try {
      const { playerId, opponentId } = req.body;

      if (!playerId || !opponentId) {
        res.status(400).json({ error: "Missing playerId or opponentId" });
        return;
      }

      const shootoutRef = await admin.firestore().collection("shootouts").add({
        playerId,
        opponentId,
        status: ShootoutStatus.IN_PROGRESS,
        createdAt: FieldValue.serverTimestamp(),
        finishedAt: null,
        winnerId: null,
      });

      logger.info("Shootout started", {
        shootoutId: shootoutRef.id,
        playerId,
        opponentId,
      });

      res.status(201).json({
        shootoutId: shootoutRef.id,
      });
    } catch (error) {
      logger.error("Error starting shootout", error);
      res.status(500).json({ error: "Internal server error" });
    }
  })
);


export const persistEvent = onRequest((req, res) => 
  corsHandler(req, res as any, async () => {
    try {
      const { shootoutId, event } = req.body;

      if (!shootoutId || !event) {
        res.status(400).json({ error: "Missing shootoutId or event" });
        return;
      }

      const shootoutRef = admin.firestore().collection("shootouts").doc(shootoutId);
      const shootoutSnap = await shootoutRef.get();

      if (!shootoutSnap.exists) {
        res.status(404).json({ error: "Shootout not found" });
        return;
      }

      await shootoutRef.collection("events").add({
        ...event,
        createdAt: FieldValue.serverTimestamp(),
      });

      logger.info("Event persisted", { shootoutId, event });

      res.status(201).json({ success: true });
    } catch (error) {
      logger.error("Error persisting event", error);
      res.status(500).json({ error: "Internal server error" });
    }
  })
);



const db = admin.firestore();

export const getShootout = onRequest((req, res) => 
  corsHandler(req, res as any, async () => {
    try {
      const shootoutId = req.query.shootoutId as string;

      if (!shootoutId) {
        res.status(400).json({ error: "shootoutId is required" });
        return;
      }

      const shootoutRef = db.collection("shootouts").doc(shootoutId);
      const shootoutSnap = await shootoutRef.get();

      if (!shootoutSnap.exists) {
        res.status(404).json({ error: "Shootout not found" });
        return;
      }

      const data = shootoutSnap.data();

      logger.info("Shootout status fetched", {
        shootoutId,
        status: data?.status,
      });

      res.status(200).json({
        id: shootoutSnap.id,
        ...data,
      });
    } catch (error) {
      logger.error("Error fetching shootout status", error);
      res.status(500).json({ error: "Internal server error" });
    }
  })
);



export const finishShootout = onRequest((req, res) => 
  corsHandler(req, res as any, async () => {
    logger.info(" +-+-+-+- finishShootout called +-+-+-+- ", { body: req.body });
    try {
      const { shootoutId, winnerName, winnerId, playerScore, aiScore } = req.body;

      if (!shootoutId || !winnerId) {
        res.status(400).json({
          error: "shootoutId and winnerId are required",
        });
        return;
      }

      const shootoutRef = db.collection("shootouts").doc(shootoutId);
      const shootoutSnap = await shootoutRef.get();

      if (!shootoutSnap.exists) {
        res.status(404).json({ error: "Shootout not found" });
        return;
      }

      const shootoutData = shootoutSnap.data();

      if (shootoutData?.status === ShootoutStatus.FINISHED) {
        res.status(409).json({ error: "Shootout already finished" });
        return;
      }

      await shootoutRef.update({
        status: ShootoutStatus.FINISHED,
        winnerId: winnerId,
        winnerName: winnerName ?? null,
        playerScore: playerScore ?? null,
        aiScore: aiScore ?? null,
        finishedAt: FieldValue.serverTimestamp(),
      });

      logger.info("Shootout finished", {
        shootoutId,
        winnerId,
        winnerName,
        playerScore,
        aiScore,
      });

      res.status(200).json({
        shootoutId,
        status: ShootoutStatus.FINISHED,
        winnerName,
      });
    } catch (error) {
      logger.error("Error finishing shootout", error);
      res.status(500).json({ error: "Internal server error" });
    }
  })
);
